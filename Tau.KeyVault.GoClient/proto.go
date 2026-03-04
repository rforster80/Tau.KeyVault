package keyvault

import (
	"fmt"
	"io"
)

// ─────────────────────────────────────────────────────────
//  Minimal protobuf encoder/decoder for wire compatibility
//  with the server's protobuf-net [ProtoContract] types.
//
//  protobuf-net uses proto3 wire format. We implement just
//  enough of the protobuf wire format to encode/decode the
//  message types used by the API, avoiding a heavy codegen
//  dependency. Field numbers match [ProtoMember(n)].
// ─────────────────────────────────────────────────────────

// Wire types
const (
	wireVarint  = 0
	wireFixed64 = 1
	wireBytes   = 2
	wireFixed32 = 5
)

// ── Encoder ──────────────────────────────────────────────

type pbEncoder struct {
	buf []byte
}

func (e *pbEncoder) writeTag(field int, wt int) {
	e.writeVarint(uint64(field<<3 | wt))
}

func (e *pbEncoder) writeVarint(v uint64) {
	for v >= 0x80 {
		e.buf = append(e.buf, byte(v)|0x80)
		v >>= 7
	}
	e.buf = append(e.buf, byte(v))
}

func (e *pbEncoder) writeString(field int, s string) {
	if s == "" {
		return
	}
	e.writeTag(field, wireBytes)
	e.writeVarint(uint64(len(s)))
	e.buf = append(e.buf, s...)
}

func (e *pbEncoder) writeBool(field int, v bool) {
	if !v {
		return
	}
	e.writeTag(field, wireVarint)
	e.buf = append(e.buf, 1)
}

func (e *pbEncoder) writeInt32(field int, v int) {
	if v == 0 {
		return
	}
	e.writeTag(field, wireVarint)
	e.writeVarint(uint64(v))
}

func (e *pbEncoder) writeBytes(field int, b []byte) {
	if len(b) == 0 {
		return
	}
	e.writeTag(field, wireBytes)
	e.writeVarint(uint64(len(b)))
	e.buf = append(e.buf, b...)
}

func (e *pbEncoder) bytes() []byte { return e.buf }

// ── Decoder ──────────────────────────────────────────────

type pbDecoder struct {
	data []byte
	pos  int
}

func newDecoder(data []byte) *pbDecoder {
	return &pbDecoder{data: data}
}

func (d *pbDecoder) done() bool { return d.pos >= len(d.data) }

func (d *pbDecoder) readVarint() (uint64, error) {
	var v uint64
	var shift uint
	for {
		if d.pos >= len(d.data) {
			return 0, io.ErrUnexpectedEOF
		}
		b := d.data[d.pos]
		d.pos++
		v |= uint64(b&0x7F) << shift
		if b < 0x80 {
			return v, nil
		}
		shift += 7
		if shift >= 64 {
			return 0, fmt.Errorf("varint overflow")
		}
	}
}

func (d *pbDecoder) readTag() (field int, wt int, err error) {
	v, err := d.readVarint()
	if err != nil {
		return 0, 0, err
	}
	return int(v >> 3), int(v & 7), nil
}

func (d *pbDecoder) readBytes() ([]byte, error) {
	length, err := d.readVarint()
	if err != nil {
		return nil, err
	}
	if d.pos+int(length) > len(d.data) {
		return nil, io.ErrUnexpectedEOF
	}
	b := d.data[d.pos : d.pos+int(length)]
	d.pos += int(length)
	return b, nil
}

func (d *pbDecoder) readString() (string, error) {
	b, err := d.readBytes()
	return string(b), err
}

func (d *pbDecoder) readBool() (bool, error) {
	v, err := d.readVarint()
	return v != 0, err
}

func (d *pbDecoder) readInt() (int, error) {
	v, err := d.readVarint()
	return int(v), err
}

func (d *pbDecoder) skipField(wt int) error {
	switch wt {
	case wireVarint:
		_, err := d.readVarint()
		return err
	case wireFixed64:
		if d.pos+8 > len(d.data) {
			return io.ErrUnexpectedEOF
		}
		d.pos += 8
		return nil
	case wireBytes:
		_, err := d.readBytes()
		return err
	case wireFixed32:
		if d.pos+4 > len(d.data) {
			return io.ErrUnexpectedEOF
		}
		d.pos += 4
		return nil
	default:
		return fmt.Errorf("unknown wire type %d", wt)
	}
}

// ── Encode functions ─────────────────────────────────────

func encodeUpsertRequest(r *upsertRequest) []byte {
	e := &pbEncoder{}
	e.writeString(1, r.Key)
	e.writeString(2, r.Value)
	e.writeString(3, r.Environment)
	e.writeString(4, r.DataType)
	e.writeBool(5, r.IsSensitive)
	return e.bytes()
}

func encodeRenameRequest(r *renameRequest) []byte {
	e := &pbEncoder{}
	e.writeString(1, r.NewName)
	return e.bytes()
}

func encodeImportKeyItem(k *ImportKeyItem) []byte {
	e := &pbEncoder{}
	e.writeString(1, k.Key)
	e.writeString(2, k.Value)
	e.writeString(3, k.DataType)
	e.writeBool(4, k.IsSensitive)
	return e.bytes()
}

func encodeImportRequest(r *ImportRequest) []byte {
	e := &pbEncoder{}
	e.writeString(1, r.Environment)
	e.writeString(2, r.Mode)
	for i := range r.Keys {
		e.writeBytes(3, encodeImportKeyItem(&r.Keys[i]))
	}
	return e.bytes()
}

// ── Decode functions ─────────────────────────────────────

func decodeKeyEntryResponse(data []byte) (*KeyEntryResponse, error) {
	d := newDecoder(data)
	r := &KeyEntryResponse{}
	for !d.done() {
		field, wt, err := d.readTag()
		if err != nil {
			return nil, err
		}
		switch field {
		case 1:
			r.Key, err = d.readString()
		case 2:
			r.Value, err = d.readString()
		case 3:
			r.Environment, err = d.readString()
		case 4:
			r.DataType, err = d.readString()
		case 5:
			r.IsSensitive, err = d.readBool()
		case 6:
			// bcl.DateTime — skip (we prefer JSON for datetime)
			err = d.skipField(wt)
		default:
			err = d.skipField(wt)
		}
		if err != nil {
			return nil, err
		}
	}
	return r, nil
}

func decodeKeyEntryListResponse(data []byte) (*KeyEntryListResponse, error) {
	d := newDecoder(data)
	r := &KeyEntryListResponse{}
	for !d.done() {
		field, wt, err := d.readTag()
		if err != nil {
			return nil, err
		}
		switch field {
		case 1:
			b, err2 := d.readBytes()
			if err2 != nil {
				return nil, err2
			}
			item, err2 := decodeKeyEntryResponse(b)
			if err2 != nil {
				return nil, err2
			}
			r.Items = append(r.Items, *item)
		default:
			err = d.skipField(wt)
		}
		if err != nil {
			return nil, err
		}
	}
	return r, nil
}

func decodeEnvironmentListResponse(data []byte) (*EnvironmentListResponse, error) {
	d := newDecoder(data)
	r := &EnvironmentListResponse{}
	for !d.done() {
		field, _, err := d.readTag()
		if err != nil {
			return nil, err
		}
		switch field {
		case 1:
			s, err2 := d.readString()
			if err2 != nil {
				return nil, err2
			}
			r.Environments = append(r.Environments, s)
		default:
			// skip
		}
	}
	return r, nil
}

func decodeDeleteEnvironmentResponse(data []byte) (*DeleteEnvironmentResponse, error) {
	d := newDecoder(data)
	r := &DeleteEnvironmentResponse{}
	for !d.done() {
		field, wt, err := d.readTag()
		if err != nil {
			return nil, err
		}
		switch field {
		case 1:
			r.Message, err = d.readString()
		case 2:
			r.DeletedKeys, err = d.readInt()
		default:
			err = d.skipField(wt)
		}
		if err != nil {
			return nil, err
		}
	}
	return r, nil
}

func decodeRenameEnvironmentResponse(data []byte) (*RenameEnvironmentResponse, error) {
	d := newDecoder(data)
	r := &RenameEnvironmentResponse{}
	for !d.done() {
		field, wt, err := d.readTag()
		if err != nil {
			return nil, err
		}
		switch field {
		case 1:
			r.Message, err = d.readString()
		case 2:
			r.UpdatedKeys, err = d.readInt()
		default:
			err = d.skipField(wt)
		}
		if err != nil {
			return nil, err
		}
	}
	return r, nil
}

func decodeExportKeyItemResponse(data []byte) (*ExportKeyItemResponse, error) {
	d := newDecoder(data)
	r := &ExportKeyItemResponse{}
	for !d.done() {
		field, wt, err := d.readTag()
		if err != nil {
			return nil, err
		}
		switch field {
		case 1:
			r.Key, err = d.readString()
		case 2:
			r.Value, err = d.readString()
		case 3:
			r.DataType, err = d.readString()
		case 4:
			r.IsSensitive, err = d.readBool()
		default:
			err = d.skipField(wt)
		}
		if err != nil {
			return nil, err
		}
	}
	return r, nil
}

func decodeExportPayloadResponse(data []byte) (*ExportPayloadResponse, error) {
	d := newDecoder(data)
	r := &ExportPayloadResponse{}
	for !d.done() {
		field, wt, err := d.readTag()
		if err != nil {
			return nil, err
		}
		switch field {
		case 1:
			r.Version, err = d.readString()
		case 2:
			// bcl.DateTime — skip
			err = d.skipField(wt)
		case 3:
			r.Environment, err = d.readString()
		case 4:
			r.KeyCount, err = d.readInt()
		case 5:
			b, err2 := d.readBytes()
			if err2 != nil {
				return nil, err2
			}
			item, err2 := decodeExportKeyItemResponse(b)
			if err2 != nil {
				return nil, err2
			}
			r.Keys = append(r.Keys, *item)
		default:
			err = d.skipField(wt)
		}
		if err != nil {
			return nil, err
		}
	}
	return r, nil
}

func decodeImportResultResponse(data []byte) (*ImportResultResponse, error) {
	d := newDecoder(data)
	r := &ImportResultResponse{}
	for !d.done() {
		field, wt, err := d.readTag()
		if err != nil {
			return nil, err
		}
		switch field {
		case 1:
			r.Imported, err = d.readInt()
		case 2:
			r.Skipped, err = d.readInt()
		case 3:
			r.Message, err = d.readString()
		default:
			err = d.skipField(wt)
		}
		if err != nil {
			return nil, err
		}
	}
	return r, nil
}

