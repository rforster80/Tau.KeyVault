package keyvault

import "fmt"

// APIError is returned when the Tau Key Vault API responds with a non-success
// HTTP status code.
type APIError struct {
	// StatusCode is the HTTP status code from the response.
	StatusCode int
	// APIMessage is the error message from the API body, if available.
	APIMessage string
}

// Error implements the error interface.
func (e *APIError) Error() string {
	if e.APIMessage != "" {
		return fmt.Sprintf("keyvault: HTTP %d — %s", e.StatusCode, e.APIMessage)
	}
	return fmt.Sprintf("keyvault: HTTP %d", e.StatusCode)
}

// IsNotFound returns true if the error represents an HTTP 404.
func (e *APIError) IsNotFound() bool {
	return e.StatusCode == 404
}

// IsNotFound reports whether err is a *APIError with HTTP 404 status.
func IsNotFound(err error) bool {
	if apiErr, ok := err.(*APIError); ok {
		return apiErr.IsNotFound()
	}
	return false
}
