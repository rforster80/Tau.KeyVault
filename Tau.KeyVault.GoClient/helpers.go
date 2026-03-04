package keyvault

import (
	"context"
	"encoding/json"
	"fmt"
	"strconv"
	"strings"
	"time"
)

// ═══════════════════════════════════════════════════════════
//  GET OR CREATE (typed, creates with default if missing)
// ═══════════════════════════════════════════════════════════

// getOrCreateRaw tries to GET a key; if 404, creates it with PUT and returns the raw value.
func (c *Client) getOrCreateRaw(
	ctx context.Context,
	key, defaultValue string,
	dataType DataType,
	environment *string,
	isSensitive bool,
) (string, error) {
	entry, err := c.GetKey(ctx, key, environment)
	if err == nil {
		return entry.Value, nil
	}
	if !IsNotFound(err) {
		return "", err
	}
	created, err := c.UpsertKey(ctx, key, defaultValue, environment, dataType, isSensitive)
	if err != nil {
		return "", err
	}
	return created.Value, nil
}

// GetOrCreateText gets a Text value, creating the key with the default if it does not exist.
func (c *Client) GetOrCreateText(ctx context.Context, key, defaultValue string, environment *string, isSensitive bool) (string, error) {
	return c.getOrCreateRaw(ctx, key, defaultValue, DataTypeText, environment, isSensitive)
}

// GetOrCreateCode gets a Code value (always uppercase), creating the key with the default if it does not exist.
func (c *Client) GetOrCreateCode(ctx context.Context, key, defaultValue string, environment *string, isSensitive bool) (string, error) {
	return c.getOrCreateRaw(ctx, key, strings.ToUpper(defaultValue), DataTypeCode, environment, isSensitive)
}

// GetOrCreateNumeric gets a Numeric (float64) value, creating the key with the default if it does not exist.
func (c *Client) GetOrCreateNumeric(ctx context.Context, key string, defaultValue float64, environment *string, isSensitive bool) (float64, error) {
	raw, err := c.getOrCreateRaw(ctx, key, strconv.FormatFloat(defaultValue, 'f', -1, 64), DataTypeNumeric, environment, isSensitive)
	if err != nil {
		return 0, err
	}
	return strconv.ParseFloat(raw, 64)
}

// GetOrCreateBoolean gets a Boolean value, creating the key with the default if it does not exist.
func (c *Client) GetOrCreateBoolean(ctx context.Context, key string, defaultValue bool, environment *string, isSensitive bool) (bool, error) {
	raw, err := c.getOrCreateRaw(ctx, key, strconv.FormatBool(defaultValue), DataTypeBoolean, environment, isSensitive)
	if err != nil {
		return false, err
	}
	return parseBool(raw), nil
}

// GetOrCreateDate gets a Date value (time.Time with date-only), creating the key with the default if it does not exist.
func (c *Client) GetOrCreateDate(ctx context.Context, key string, defaultValue time.Time, environment *string, isSensitive bool) (time.Time, error) {
	raw, err := c.getOrCreateRaw(ctx, key, defaultValue.Format("2006-01-02"), DataTypeDate, environment, isSensitive)
	if err != nil {
		return time.Time{}, err
	}
	return time.Parse("2006-01-02", raw)
}

// GetOrCreateTime gets a Time value (time.Time with time-only), creating the key with the default if it does not exist.
func (c *Client) GetOrCreateTime(ctx context.Context, key string, defaultValue time.Time, environment *string, isSensitive bool) (time.Time, error) {
	raw, err := c.getOrCreateRaw(ctx, key, defaultValue.Format("15:04:05"), DataTypeTime, environment, isSensitive)
	if err != nil {
		return time.Time{}, err
	}
	return time.Parse("15:04:05", raw)
}

// GetOrCreateDateTime gets a DateTime value, creating the key with the default if it does not exist.
func (c *Client) GetOrCreateDateTime(ctx context.Context, key string, defaultValue time.Time, environment *string, isSensitive bool) (time.Time, error) {
	raw, err := c.getOrCreateRaw(ctx, key, defaultValue.Format("2006-01-02T15:04:05"), DataTypeDateTime, environment, isSensitive)
	if err != nil {
		return time.Time{}, err
	}
	return time.Parse("2006-01-02T15:04:05", raw)
}

// GetOrCreateJSON gets a JSON value deserialized into target, creating the key with the
// serialized default if it does not exist.
func (c *Client) GetOrCreateJSON(ctx context.Context, key string, defaultValue any, target any, environment *string, isSensitive bool) error {
	defaultJSON, err := json.Marshal(defaultValue)
	if err != nil {
		return fmt.Errorf("keyvault: marshal default JSON: %w", err)
	}
	raw, err := c.getOrCreateRaw(ctx, key, string(defaultJSON), DataTypeJSON, environment, isSensitive)
	if err != nil {
		return err
	}
	return json.Unmarshal([]byte(raw), target)
}

// GetOrCreateCSV gets a CSV value as a string slice, creating the key with the
// default list if it does not exist.
func (c *Client) GetOrCreateCSV(ctx context.Context, key string, defaultValues []string, environment *string, isSensitive bool) ([]string, error) {
	raw, err := c.getOrCreateRaw(ctx, key, strings.Join(defaultValues, ","), DataTypeCSV, environment, isSensitive)
	if err != nil {
		return nil, err
	}
	return parseCSV(raw), nil
}

// ═══════════════════════════════════════════════════════════
//  TYPED GET HELPERS
// ═══════════════════════════════════════════════════════════

// GetText gets a Text value.
func (c *Client) GetText(ctx context.Context, key string, environment *string) (string, error) {
	entry, err := c.GetKey(ctx, key, environment)
	if err != nil {
		return "", err
	}
	return entry.Value, nil
}

// GetCode gets a Code value (uppercase text).
func (c *Client) GetCode(ctx context.Context, key string, environment *string) (string, error) {
	entry, err := c.GetKey(ctx, key, environment)
	if err != nil {
		return "", err
	}
	return entry.Value, nil
}

// GetNumeric gets a Numeric value as float64.
func (c *Client) GetNumeric(ctx context.Context, key string, environment *string) (float64, error) {
	entry, err := c.GetKey(ctx, key, environment)
	if err != nil {
		return 0, err
	}
	return strconv.ParseFloat(entry.Value, 64)
}

// GetBoolean gets a Boolean value.
func (c *Client) GetBoolean(ctx context.Context, key string, environment *string) (bool, error) {
	entry, err := c.GetKey(ctx, key, environment)
	if err != nil {
		return false, err
	}
	return parseBool(entry.Value), nil
}

// GetDate gets a Date value as time.Time.
func (c *Client) GetDate(ctx context.Context, key string, environment *string) (time.Time, error) {
	entry, err := c.GetKey(ctx, key, environment)
	if err != nil {
		return time.Time{}, err
	}
	return time.Parse("2006-01-02", entry.Value)
}

// GetTime gets a Time value as time.Time.
func (c *Client) GetTime(ctx context.Context, key string, environment *string) (time.Time, error) {
	entry, err := c.GetKey(ctx, key, environment)
	if err != nil {
		return time.Time{}, err
	}
	return time.Parse("15:04:05", entry.Value)
}

// GetDateTime gets a DateTime value as time.Time.
func (c *Client) GetDateTime(ctx context.Context, key string, environment *string) (time.Time, error) {
	entry, err := c.GetKey(ctx, key, environment)
	if err != nil {
		return time.Time{}, err
	}
	return time.Parse("2006-01-02T15:04:05", entry.Value)
}

// GetJSON gets a JSON value deserialized into the provided target.
func (c *Client) GetJSON(ctx context.Context, key string, target any, environment *string) error {
	entry, err := c.GetKey(ctx, key, environment)
	if err != nil {
		return err
	}
	return json.Unmarshal([]byte(entry.Value), target)
}

// GetCSV gets a CSV value as a string slice.
func (c *Client) GetCSV(ctx context.Context, key string, environment *string) ([]string, error) {
	entry, err := c.GetKey(ctx, key, environment)
	if err != nil {
		return nil, err
	}
	return parseCSV(entry.Value), nil
}

// ═══════════════════════════════════════════════════════════
//  TYPED UPDATE HELPERS
// ═══════════════════════════════════════════════════════════

// UpdateText updates a Text value.
func (c *Client) UpdateText(ctx context.Context, key, value string, environment *string, isSensitive bool) (*KeyEntryResponse, error) {
	return c.UpsertKey(ctx, key, value, environment, DataTypeText, isSensitive)
}

// UpdateCode updates a Code value (stored uppercase).
func (c *Client) UpdateCode(ctx context.Context, key, value string, environment *string, isSensitive bool) (*KeyEntryResponse, error) {
	return c.UpsertKey(ctx, key, strings.ToUpper(value), environment, DataTypeCode, isSensitive)
}

// UpdateNumeric updates a Numeric value.
func (c *Client) UpdateNumeric(ctx context.Context, key string, value float64, environment *string, isSensitive bool) (*KeyEntryResponse, error) {
	return c.UpsertKey(ctx, key, strconv.FormatFloat(value, 'f', -1, 64), environment, DataTypeNumeric, isSensitive)
}

// UpdateBoolean updates a Boolean value.
func (c *Client) UpdateBoolean(ctx context.Context, key string, value bool, environment *string, isSensitive bool) (*KeyEntryResponse, error) {
	return c.UpsertKey(ctx, key, strconv.FormatBool(value), environment, DataTypeBoolean, isSensitive)
}

// UpdateDate updates a Date value.
func (c *Client) UpdateDate(ctx context.Context, key string, value time.Time, environment *string, isSensitive bool) (*KeyEntryResponse, error) {
	return c.UpsertKey(ctx, key, value.Format("2006-01-02"), environment, DataTypeDate, isSensitive)
}

// UpdateTime updates a Time value.
func (c *Client) UpdateTime(ctx context.Context, key string, value time.Time, environment *string, isSensitive bool) (*KeyEntryResponse, error) {
	return c.UpsertKey(ctx, key, value.Format("15:04:05"), environment, DataTypeTime, isSensitive)
}

// UpdateDateTime updates a DateTime value.
func (c *Client) UpdateDateTime(ctx context.Context, key string, value time.Time, environment *string, isSensitive bool) (*KeyEntryResponse, error) {
	return c.UpsertKey(ctx, key, value.Format("2006-01-02T15:04:05"), environment, DataTypeDateTime, isSensitive)
}

// UpdateJSON updates a JSON value by marshaling the object.
func (c *Client) UpdateJSON(ctx context.Context, key string, value any, environment *string, isSensitive bool) (*KeyEntryResponse, error) {
	b, err := json.Marshal(value)
	if err != nil {
		return nil, fmt.Errorf("keyvault: marshal JSON: %w", err)
	}
	return c.UpsertKey(ctx, key, string(b), environment, DataTypeJSON, isSensitive)
}

// UpdateCSV updates a CSV value from a string slice.
func (c *Client) UpdateCSV(ctx context.Context, key string, values []string, environment *string, isSensitive bool) (*KeyEntryResponse, error) {
	return c.UpsertKey(ctx, key, strings.Join(values, ","), environment, DataTypeCSV, isSensitive)
}

// ═══════════════════════════════════════════════════════════
//  CSV LIST MANAGEMENT HELPERS
// ═══════════════════════════════════════════════════════════

// CSVAdd adds an item to a CSV list key. Creates the key if it does not exist.
// Returns the updated list.
func (c *Client) CSVAdd(ctx context.Context, key, item string, environment *string, isSensitive bool) ([]string, error) {
	list, err := c.GetOrCreateCSV(ctx, key, nil, environment, isSensitive)
	if err != nil {
		return nil, err
	}
	list = append(list, item)
	if _, err := c.UpdateCSV(ctx, key, list, environment, isSensitive); err != nil {
		return nil, err
	}
	return list, nil
}

// CSVRemove removes the first occurrence of an item from a CSV list key.
// Returns the updated list.
func (c *Client) CSVRemove(ctx context.Context, key, item string, environment *string, isSensitive bool) ([]string, error) {
	list, err := c.GetCSV(ctx, key, environment)
	if err != nil {
		return nil, err
	}
	for i, v := range list {
		if v == item {
			list = append(list[:i], list[i+1:]...)
			break
		}
	}
	if _, err := c.UpdateCSV(ctx, key, list, environment, isSensitive); err != nil {
		return nil, err
	}
	return list, nil
}

// CSVContains checks if a CSV list key contains an item.
func (c *Client) CSVContains(ctx context.Context, key, item string, environment *string) (bool, error) {
	list, err := c.GetCSV(ctx, key, environment)
	if err != nil {
		return false, err
	}
	for _, v := range list {
		if v == item {
			return true, nil
		}
	}
	return false, nil
}

// CSVReplace replaces all occurrences of oldItem with newItem in a CSV list key.
// Returns the updated list.
func (c *Client) CSVReplace(ctx context.Context, key, oldItem, newItem string, environment *string, isSensitive bool) ([]string, error) {
	list, err := c.GetCSV(ctx, key, environment)
	if err != nil {
		return nil, err
	}
	for i, v := range list {
		if v == oldItem {
			list[i] = newItem
		}
	}
	if _, err := c.UpdateCSV(ctx, key, list, environment, isSensitive); err != nil {
		return nil, err
	}
	return list, nil
}

// ═══════════════════════════════════════════════════════════
//  Internal utilities
// ═══════════════════════════════════════════════════════════

// parseBool interprets "true", "1", "yes" (case-insensitive) as true.
func parseBool(s string) bool {
	v := strings.TrimSpace(strings.ToLower(s))
	return v == "true" || v == "1" || v == "yes"
}

// parseCSV splits a CSV string into trimmed, non-empty entries.
func parseCSV(s string) []string {
	if strings.TrimSpace(s) == "" {
		return nil
	}
	parts := strings.Split(s, ",")
	result := make([]string, 0, len(parts))
	for _, p := range parts {
		p = strings.TrimSpace(p)
		if p != "" {
			result = append(result, p)
		}
	}
	return result
}

// Env is a convenience function to create a *string for the environment parameter.
func Env(s string) *string {
	return &s
}
