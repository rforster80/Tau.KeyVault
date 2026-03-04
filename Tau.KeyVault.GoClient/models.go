package keyvault

// ═══════════════════════════════════════════════════════════
//  Response models
// ═══════════════════════════════════════════════════════════

// KeyEntryResponse represents a single key-value entry.
type KeyEntryResponse struct {
	Key         string `json:"key"         protobuf:"bytes,1,opt,name=key"`
	Value       string `json:"value"       protobuf:"bytes,2,opt,name=value"`
	Environment string `json:"environment" protobuf:"bytes,3,opt,name=environment"`
	DataType    string `json:"dataType"    protobuf:"bytes,4,opt,name=dataType"`
	IsSensitive bool   `json:"isSensitive" protobuf:"varint,5,opt,name=isSensitive"`
	UpdatedAt   string `json:"updatedAt"   protobuf:"bytes,6,opt,name=updatedAt"`
}

// KeyEntryListResponse is a list of key-value entries.
type KeyEntryListResponse struct {
	Items []KeyEntryResponse `json:"items" protobuf:"bytes,1,rep,name=items"`
}

// EnvironmentListResponse is a list of environment names.
type EnvironmentListResponse struct {
	Environments []string `json:"environments" protobuf:"bytes,1,rep,name=environments"`
}

// DeleteEnvironmentResponse is the result of deleting an environment.
type DeleteEnvironmentResponse struct {
	Message     string `json:"message"     protobuf:"bytes,1,opt,name=message"`
	DeletedKeys int    `json:"deletedKeys" protobuf:"varint,2,opt,name=deletedKeys"`
}

// RenameEnvironmentResponse is the result of renaming an environment.
type RenameEnvironmentResponse struct {
	Message     string `json:"message"     protobuf:"bytes,1,opt,name=message"`
	UpdatedKeys int    `json:"updatedKeys" protobuf:"varint,2,opt,name=updatedKeys"`
}

// ExportKeyItemResponse is a single key in an export payload.
type ExportKeyItemResponse struct {
	Key         string `json:"key"         protobuf:"bytes,1,opt,name=key"`
	Value       string `json:"value"       protobuf:"bytes,2,opt,name=value"`
	DataType    string `json:"dataType"    protobuf:"bytes,3,opt,name=dataType"`
	IsSensitive bool   `json:"isSensitive" protobuf:"varint,4,opt,name=isSensitive"`
}

// ExportPayloadResponse is the full export payload.
type ExportPayloadResponse struct {
	Version     string                  `json:"version"     protobuf:"bytes,1,opt,name=version"`
	ExportDate  string                  `json:"exportDate"  protobuf:"bytes,2,opt,name=exportDate"`
	Environment string                  `json:"environment" protobuf:"bytes,3,opt,name=environment"`
	KeyCount    int                     `json:"keyCount"    protobuf:"varint,4,opt,name=keyCount"`
	Keys        []ExportKeyItemResponse `json:"keys"        protobuf:"bytes,5,rep,name=keys"`
}

// ImportResultResponse is the result of an import operation.
type ImportResultResponse struct {
	Imported int    `json:"imported" protobuf:"varint,1,opt,name=imported"`
	Skipped  int    `json:"skipped"  protobuf:"varint,2,opt,name=skipped"`
	Message  string `json:"message"  protobuf:"bytes,3,opt,name=message"`
}

// errorResponse is used internally to parse API error bodies.
type errorResponse struct {
	Error string `json:"error" protobuf:"bytes,1,opt,name=error"`
}

// ═══════════════════════════════════════════════════════════
//  Request models
// ═══════════════════════════════════════════════════════════

// upsertRequest is the body for creating or updating a key.
type upsertRequest struct {
	Key         string `json:"key"         protobuf:"bytes,1,opt,name=key"`
	Value       string `json:"value"       protobuf:"bytes,2,opt,name=value"`
	Environment string `json:"environment" protobuf:"bytes,3,opt,name=environment"`
	DataType    string `json:"dataType"    protobuf:"bytes,4,opt,name=dataType"`
	IsSensitive bool   `json:"isSensitive" protobuf:"varint,5,opt,name=isSensitive"`
}

// renameRequest is the body for renaming an environment.
type renameRequest struct {
	NewName string `json:"newName" protobuf:"bytes,1,opt,name=newName"`
}

// ImportKeyItem is a single key for an import payload.
type ImportKeyItem struct {
	Key         string `json:"key"         protobuf:"bytes,1,opt,name=key"`
	Value       string `json:"value"       protobuf:"bytes,2,opt,name=value"`
	DataType    string `json:"dataType"    protobuf:"bytes,3,opt,name=dataType"`
	IsSensitive bool   `json:"isSensitive" protobuf:"varint,4,opt,name=isSensitive"`
}

// ImportRequest is the payload for importing keys.
type ImportRequest struct {
	Environment string          `json:"environment" protobuf:"bytes,1,opt,name=environment"`
	Mode        string          `json:"mode"        protobuf:"bytes,2,opt,name=mode"`
	Keys        []ImportKeyItem `json:"keys"        protobuf:"bytes,3,rep,name=keys"`
}
