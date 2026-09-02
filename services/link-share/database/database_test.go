package database

import (
	"fmt"
	"testing"

	"github.com/aiguoli/SimpleList/services/link-share/models"
	"github.com/glebarez/sqlite"
	"gorm.io/gorm"
)

func TestMigrateBackfillsLegacyLinks(t *testing.T) {
	dsn := fmt.Sprintf("file:%s?mode=memory&cache=shared", t.Name())
	db, err := gorm.Open(sqlite.Open(dsn), &gorm.Config{})
	if err != nil {
		t.Fatal(err)
	}
	if err := db.AutoMigrate(&models.Link{}); err != nil {
		t.Fatal(err)
	}
	legacy := models.Link{Title: "legacy", Content: "https://example.com", ProviderType: ""}
	if err := db.Create(&legacy).Error; err != nil {
		t.Fatal(err)
	}
	if err := db.Model(&legacy).UpdateColumn("provider_type", "").Error; err != nil {
		t.Fatal(err)
	}
	if err := Migrate(db); err != nil {
		t.Fatal(err)
	}
	var migrated models.Link
	if err := db.First(&migrated, legacy.ID).Error; err != nil {
		t.Fatal(err)
	}
	if migrated.ProviderType != models.ProviderOneDrive {
		t.Fatalf("provider_type = %q", migrated.ProviderType)
	}
}
