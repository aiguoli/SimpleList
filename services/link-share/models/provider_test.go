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
		"Share Point":   ProviderSharePoint,
	}
	for input, expected := range tests {
		if actual := NormalizeProviderType(input); actual != expected {
			t.Fatalf("NormalizeProviderType(%q) = %q, want %q", input, actual, expected)
		}
	}
}

func TestSupportedProviders(t *testing.T) {
	if !IsSupportedProvider(ProviderOneDrive) || !IsSupportedProvider(ProviderGoogleDrive) || !IsSupportedProvider(ProviderSharePoint) {
		t.Fatal("expected OneDrive, Google Drive, and SharePoint to be supported")
	}
	if IsSupportedProvider("local") || IsSupportedProvider("pikpak") {
		t.Fatal("local and PikPak do not currently support share links")
	}
}
