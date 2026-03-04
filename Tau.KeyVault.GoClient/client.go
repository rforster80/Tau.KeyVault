package keyvault

import (
	"bytes"
	"context"
	"encoding/json"
	"fmt"
	"io"
	"net/http"
	"net/url"
	"strings"
	"time"
)

const (
	protoContentType = "application/x-protobuf"
	jsonContentType  = "application/json"
)

// Options configures a KeyVaultClient.
type Options struct {
	// BaseURL is the root URL of the Tau Key Vault server (e.g. "https://localhost:5001").
	BaseURL string
	// APIKey is sent as the X-Api-Key header for authentication.
	APIKey string
	// DefaultEnvironment is the environment used when none is specified.
	// An empty string represents the Global environment.
	DefaultEnvironment string
	// Transport determines JSON vs Protobuf vs fallback mode. Default: TransportAPI.
	Transport Transport
	// Timeout for HTTP requests. Default: 30 seconds.
	Timeout time.Duration
	// HTTPClient allows injecting a custom *http.Client. If nil, a default is created.
	HTTPClient *http.Client
}

// Client is the main entry point for the Tau Key Vault REST API.
type Client struct {
	baseURL    string
	apiKey     string
	defaultEnv string
	transport  Transport
	http       *http.Client
}

// NewClient creates a new Tau Key Vault client with the given options.
func NewClient(opts Options) (*Client, error) {
	if opts.BaseURL == "" {
		return nil, fmt.Errorf("keyvault: BaseURL is required")
	}
	if opts.APIKey == "" {
		return nil, fmt.Errorf("keyvault: APIKey is required")
	}

	timeout := opts.Timeout
	if timeout == 0 {
		timeout = 30 * time.Second
	}

	httpClient := opts.HTTPClient
	if httpClient == nil {
		httpClient = &http.Client{Timeout: timeout}
	}

	return &Client{
		baseURL:    strings.TrimRight(opts.BaseURL, "/"),
		apiKey:     opts.APIKey,
		defaultEnv: opts.DefaultEnvironment,
		transport:  opts.Transport,
		http:       httpClient,
	}, nil
}

// env resolves the environment to use, falling back to the default.
func (c *Client) env(environment *string) string {
	if environment != nil {
		return *environment
	}
	return c.defaultEnv
}

// ═══════════════════════════════════════════════════════════
//  CORE API METHODS (Swagger endpoints)
// ═══════════════════════════════════════════════════════════

// GetAllKeys lists all keys for an environment with global fallback.
// Pass nil for environment to use the default.
func (c *Client) GetAllKeys(ctx context.Context, environment *string, raw bool) (*KeyEntryListResponse, error) {
	env := c.env(environment)
	path := fmt.Sprintf("api/keys?environment=%s&raw=%t", url.QueryEscape(env), raw)
	return sendGet(c, ctx, path, decodeKeyEntryListResponse)
}

// GetKey gets a single key by name with global fallback.
func (c *Client) GetKey(ctx context.Context, key string, environment *string) (*KeyEntryResponse, error) {
	env := c.env(environment)
	path := fmt.Sprintf("api/keys/%s?environment=%s", url.PathEscape(key), url.QueryEscape(env))
	return sendGet(c, ctx, path, decodeKeyEntryResponse)
}

// UpsertKey creates or updates a key-value pair.
func (c *Client) UpsertKey(
	ctx context.Context,
	key, value string,
	environment *string,
	dataType DataType,
	isSensitive bool,
) (*KeyEntryResponse, error) {
	env := c.env(environment)
	req := &upsertRequest{
		Key: key, Value: value, Environment: env,
		DataType: string(dataType), IsSensitive: isSensitive,
	}
	return sendPut(c, ctx, "api/keys", req, encodeUpsertRequest, decodeKeyEntryResponse)
}

// GetEnvironments lists all known environments.
func (c *Client) GetEnvironments(ctx context.Context) (*EnvironmentListResponse, error) {
	return sendGet(c, ctx, "api/keys/environments", decodeEnvironmentListResponse)
}

// DeleteEnvironment deletes an environment and all its keys.
func (c *Client) DeleteEnvironment(ctx context.Context, environment string) (*DeleteEnvironmentResponse, error) {
	path := fmt.Sprintf("api/keys/environments/%s", url.PathEscape(environment))
	return sendDelete(c, ctx, path, decodeDeleteEnvironmentResponse)
}

// RenameEnvironment renames an environment.
func (c *Client) RenameEnvironment(ctx context.Context, environment, newName string) (*RenameEnvironmentResponse, error) {
	path := fmt.Sprintf("api/keys/environments/%s/rename", url.PathEscape(environment))
	req := &renameRequest{NewName: newName}
	return sendPut(c, ctx, path, req, encodeRenameRequest, decodeRenameEnvironmentResponse)
}

// Export exports all keys for an environment.
func (c *Client) Export(ctx context.Context, environment *string) (*ExportPayloadResponse, error) {
	env := c.env(environment)
	path := fmt.Sprintf("api/keys/export?environment=%s", url.QueryEscape(env))
	return sendGet(c, ctx, path, decodeExportPayloadResponse)
}

// Import imports keys into an environment.
func (c *Client) Import(ctx context.Context, request *ImportRequest) (*ImportResultResponse, error) {
	return sendPost(c, ctx, "api/keys/import", request, encodeImportRequest, decodeImportResultResponse)
}

// GetProtoSchema downloads the auto-generated .proto schema file as a string.
func (c *Client) GetProtoSchema(ctx context.Context) (string, error) {
	resp, err := c.rawRequest(ctx, http.MethodGet, "api/keys/proto", "text/plain", nil, "")
	if err != nil {
		return "", err
	}
	defer resp.Body.Close()
	body, err := io.ReadAll(resp.Body)
	if err != nil {
		return "", err
	}
	return string(body), nil
}

// ═══════════════════════════════════════════════════════════
//  KEY EXISTS
// ═══════════════════════════════════════════════════════════

// KeyExists checks if a key exists in the specified environment (with global fallback).
func (c *Client) KeyExists(ctx context.Context, key string, environment *string) (bool, error) {
	_, err := c.GetKey(ctx, key, environment)
	if err == nil {
		return true, nil
	}
	if IsNotFound(err) {
		return false, nil
	}
	return false, err
}

// ═══════════════════════════════════════════════════════════
//  INTERNAL: Transport dispatch
// ═══════════════════════════════════════════════════════════

// sendGet dispatches a GET with the configured transport mode.
func sendGet[T any](
	c *Client, ctx context.Context, path string,
	protoDecode func([]byte) (*T, error),
) (*T, error) {
	switch c.transport {
	case TransportProtobuf:
		return execGet(c, ctx, path, true, protoDecode)
	case TransportProtobufWithAPIFallback:
		r, err := execGet(c, ctx, path, true, protoDecode)
		if err == nil {
			return r, nil
		}
		return execGet(c, ctx, path, false, protoDecode)
	default:
		return execGet(c, ctx, path, false, protoDecode)
	}
}

func sendPut[TReq any, TResp any](
	c *Client, ctx context.Context, path string,
	body *TReq,
	protoEncode func(*TReq) []byte,
	protoDecode func([]byte) (*TResp, error),
) (*TResp, error) {
	switch c.transport {
	case TransportProtobuf:
		return execMutate(c, ctx, http.MethodPut, path, body, true, protoEncode, protoDecode)
	case TransportProtobufWithAPIFallback:
		r, err := execMutate(c, ctx, http.MethodPut, path, body, true, protoEncode, protoDecode)
		if err == nil {
			return r, nil
		}
		return execMutate(c, ctx, http.MethodPut, path, body, false, protoEncode, protoDecode)
	default:
		return execMutate(c, ctx, http.MethodPut, path, body, false, protoEncode, protoDecode)
	}
}

func sendPost[TReq any, TResp any](
	c *Client, ctx context.Context, path string,
	body *TReq,
	protoEncode func(*TReq) []byte,
	protoDecode func([]byte) (*TResp, error),
) (*TResp, error) {
	switch c.transport {
	case TransportProtobuf:
		return execMutate(c, ctx, http.MethodPost, path, body, true, protoEncode, protoDecode)
	case TransportProtobufWithAPIFallback:
		r, err := execMutate(c, ctx, http.MethodPost, path, body, true, protoEncode, protoDecode)
		if err == nil {
			return r, nil
		}
		return execMutate(c, ctx, http.MethodPost, path, body, false, protoEncode, protoDecode)
	default:
		return execMutate(c, ctx, http.MethodPost, path, body, false, protoEncode, protoDecode)
	}
}

func sendDelete[T any](
	c *Client, ctx context.Context, path string,
	protoDecode func([]byte) (*T, error),
) (*T, error) {
	switch c.transport {
	case TransportProtobuf:
		return execGet(c, ctx, path, true, protoDecode) // DELETE uses same pattern as GET (no body)
	case TransportProtobufWithAPIFallback:
		r, err := execDelete(c, ctx, path, true, protoDecode)
		if err == nil {
			return r, nil
		}
		return execDelete(c, ctx, path, false, protoDecode)
	default:
		return execDelete(c, ctx, path, false, protoDecode)
	}
}

// ── Core executors ───────────────────────────────────────

func execGet[T any](
	c *Client, ctx context.Context, path string,
	useProtobuf bool,
	protoDecode func([]byte) (*T, error),
) (*T, error) {
	accept := jsonContentType
	if useProtobuf {
		accept = protoContentType
	}
	resp, err := c.rawRequest(ctx, http.MethodGet, path, accept, nil, "")
	if err != nil {
		return nil, err
	}
	defer resp.Body.Close()
	return handleResponse(resp, useProtobuf, protoDecode)
}

func execDelete[T any](
	c *Client, ctx context.Context, path string,
	useProtobuf bool,
	protoDecode func([]byte) (*T, error),
) (*T, error) {
	accept := jsonContentType
	if useProtobuf {
		accept = protoContentType
	}
	resp, err := c.rawRequest(ctx, http.MethodDelete, path, accept, nil, "")
	if err != nil {
		return nil, err
	}
	defer resp.Body.Close()
	return handleResponse(resp, useProtobuf, protoDecode)
}

func execMutate[TReq any, TResp any](
	c *Client, ctx context.Context,
	method, path string,
	body *TReq,
	useProtobuf bool,
	protoEncode func(*TReq) []byte,
	protoDecode func([]byte) (*TResp, error),
) (*TResp, error) {
	var bodyReader io.Reader
	var contentType string
	var accept string

	if useProtobuf {
		bodyReader = bytes.NewReader(protoEncode(body))
		contentType = protoContentType
		accept = protoContentType
	} else {
		jsonBytes, err := json.Marshal(body)
		if err != nil {
			return nil, fmt.Errorf("keyvault: marshal JSON: %w", err)
		}
		bodyReader = bytes.NewReader(jsonBytes)
		contentType = jsonContentType
		accept = jsonContentType
	}

	resp, err := c.rawRequest(ctx, method, path, accept, bodyReader, contentType)
	if err != nil {
		return nil, err
	}
	defer resp.Body.Close()
	return handleResponse(resp, useProtobuf, protoDecode)
}

// ── Raw HTTP ─────────────────────────────────────────────

func (c *Client) rawRequest(
	ctx context.Context,
	method, path, accept string,
	body io.Reader,
	contentType string,
) (*http.Response, error) {
	fullURL := c.baseURL + "/" + path
	req, err := http.NewRequestWithContext(ctx, method, fullURL, body)
	if err != nil {
		return nil, fmt.Errorf("keyvault: create request: %w", err)
	}
	req.Header.Set("X-Api-Key", c.apiKey)
	req.Header.Set("Accept", accept)
	if contentType != "" {
		req.Header.Set("Content-Type", contentType)
	}

	resp, err := c.http.Do(req)
	if err != nil {
		return nil, fmt.Errorf("keyvault: http request: %w", err)
	}

	if resp.StatusCode >= 400 {
		defer resp.Body.Close()
		respBody, _ := io.ReadAll(resp.Body)
		apiMsg := ""
		var errResp errorResponse
		if json.Unmarshal(respBody, &errResp) == nil && errResp.Error != "" {
			apiMsg = errResp.Error
		}
		return nil, &APIError{StatusCode: resp.StatusCode, APIMessage: apiMsg}
	}

	return resp, nil
}

// ── Response handling ────────────────────────────────────

func handleResponse[T any](
	resp *http.Response,
	useProtobuf bool,
	protoDecode func([]byte) (*T, error),
) (*T, error) {
	body, err := io.ReadAll(resp.Body)
	if err != nil {
		return nil, fmt.Errorf("keyvault: read response: %w", err)
	}
	if useProtobuf {
		return protoDecode(body)
	}
	var result T
	if err := json.Unmarshal(body, &result); err != nil {
		return nil, fmt.Errorf("keyvault: unmarshal JSON: %w", err)
	}
	return &result, nil
}
