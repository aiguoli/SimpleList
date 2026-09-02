package database

import (
	"fmt"
	"github.com/aiguoli/SimpleList/services/link-share/models"
	"github.com/glebarez/sqlite"
	"gorm.io/gorm"
	"os"
	"time"
)

var DB *gorm.DB

func ConnectDB() error {
	databasePath := os.Getenv("DATABASE_PATH")
	if databasePath == "" {
		databasePath = "data.db"
	}
	db, err := gorm.Open(sqlite.Open(databasePath), &gorm.Config{
		DisableForeignKeyConstraintWhenMigrating: true,
	})

	if err != nil {
		return fmt.Errorf("connect database: %w", err)
	}

	if err := Migrate(db); err != nil {
		return err
	}
	DB = db
	return nil
}

// Migrate keeps schema changes explicit and makes legacy link-share databases
// safe to open with v2. Version 1 adds provider-neutral links and refresh-token
// sessions; pre-v2 links are OneDrive links and are backfilled accordingly.
func Migrate(db *gorm.DB) error {
	if err := db.AutoMigrate(
		&models.User{},
		&models.Link{},
		&models.Collection{},
		&models.Category{},
		&models.RefreshToken{},
		&models.SchemaMigration{},
	); err != nil {
		return fmt.Errorf("auto migrate: %w", err)
	}

	return db.Transaction(func(tx *gorm.DB) error {
		var applied int64
		if err := tx.Model(&models.SchemaMigration{}).Where("version = ?", 1).Count(&applied).Error; err != nil {
			return err
		}
		if applied > 0 {
			return nil
		}
		if err := tx.Model(&models.Link{}).
			Where("provider_type IS NULL OR TRIM(provider_type) = ''").
			Update("provider_type", models.ProviderOneDrive).Error; err != nil {
			return err
		}
		return tx.Create(&models.SchemaMigration{Version: 1, AppliedAt: time.Now().UTC()}).Error
	})
}
