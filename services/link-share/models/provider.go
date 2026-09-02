package models

import "strings"

const (
	ProviderOneDrive    = "onedrive"
	ProviderGoogleDrive = "google_drive"
	ProviderLocal       = "local"
	ProviderPikPak      = "pikpak"
)

// NormalizeProviderType converts client-facing aliases to the stable v2 values.
func NormalizeProviderType(value string) string {
	normalized := strings.ToLower(strings.TrimSpace(value))
	normalized = strings.NewReplacer("-", "_", " ", "_").Replace(normalized)

	switch normalized {
	case "onedrive", "one_drive":
		return ProviderOneDrive
	case "googledrive", "google_drive":
		return ProviderGoogleDrive
	case "local", "local_storage":
		return ProviderLocal
	case "pikpak", "pik_pak":
		return ProviderPikPak
	default:
		return normalized
	}
}

// IsSupportedProvider reports whether a provider can currently publish a
// public link to the community. Local storage and PikPak are still returned by
// the providers endpoint so clients can explain why publishing is disabled.
func IsSupportedProvider(value string) bool {
	switch NormalizeProviderType(value) {
	case ProviderOneDrive, ProviderGoogleDrive:
		return true
	default:
		return false
	}
}
