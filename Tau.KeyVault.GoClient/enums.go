// Package keyvault provides a Go client for the Tau Key Vault REST API.
// It supports JSON and Protocol Buffers transport with typed helper methods
// for all nine key-value data types.
package keyvault

// Transport determines how the client communicates with the Tau Key Vault API.
type Transport int

const (
	// TransportAPI uses JSON for all requests and responses (default).
	TransportAPI Transport = iota
	// TransportProtobuf uses Protocol Buffers for all requests and responses.
	TransportProtobuf
	// TransportProtobufWithAPIFallback attempts Protobuf first; on failure, retries with JSON.
	TransportProtobufWithAPIFallback
)

// DataType represents the data types supported by Tau Key Vault.
type DataType string

const (
	DataTypeText     DataType = "Text"
	DataTypeCode     DataType = "Code"
	DataTypeNumeric  DataType = "Numeric"
	DataTypeBoolean  DataType = "Boolean"
	DataTypeDate     DataType = "Date"
	DataTypeTime     DataType = "Time"
	DataTypeDateTime DataType = "DateTime"
	DataTypeJSON     DataType = "Json"
	DataTypeCSV      DataType = "Csv"
)
