package models

import "testing"

func TestNormalizeProviderType(t *testing.T) {
	tests := map[string]string{
		"OneDrive":      ProviderOneDrive,
		"one_drive":     ProviderOneDrive,
		"Google Drive":  ProviderGoogleDrive,
		"google-drive":  ProviderGoogleDrive,
		"Local Storage": ProviderLocal,
		"pik-pak":       ProviderPikPak,
	}
	for input, expected := range tests {
		if actual := NormalizeProviderType(input); actual != expected {
			t.Fatalf("NormalizeProviderType(%q) = %q, want %q", input, actual, expected)
		}
	}
}

func TestSupportedProviders(t *testing.T) {
	if !IsSupportedProvider(ProviderOneDrive) || !IsSupportedProvider(ProviderGoogleDrive) {
		t.Fatal("expected OneDrive and Google Drive to be supported")
	}
	if IsSupportedProvider("local") || IsSupportedProvider("pikpak") {
		t.Fatal("local and PikPak do not currently support share links")
	}
}
