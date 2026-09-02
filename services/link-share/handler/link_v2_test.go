package handler

import (
	"testing"
	"time"
)

func TestParseExpirationV2(t *testing.T) {
	date, err := parseExpirationV2("2026-08-31")
	if err != nil || date.Format("2006-01-02") != "2026-08-31" {
		t.Fatalf("unexpected date result: %v, %v", date, err)
	}

	timestamp, err := parseExpirationV2("2026-08-31T10:30:00Z")
	if err != nil || timestamp.Location() != time.UTC {
		t.Fatalf("unexpected timestamp result: %v, %v", timestamp, err)
	}

	if _, err := parseExpirationV2("tomorrow"); err == nil {
		t.Fatal("expected an invalid expiration to fail")
	}
}

func TestIsPublicHTTPURL(t *testing.T) {
	for _, value := range []string{"https://example.com/file", "http://example.com"} {
		if !isPublicHTTPURL(value) {
			t.Fatalf("expected %q to be accepted", value)
		}
	}
	for _, value := range []string{"", "example.com", "file:///tmp/file", "javascript:alert(1)"} {
		if isPublicHTTPURL(value) {
			t.Fatalf("expected %q to be rejected", value)
		}
	}
}
