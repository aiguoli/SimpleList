package models

import (
	"time"

	"gorm.io/gorm"
)

// RefreshToken stores only a SHA-256 hash of the opaque token returned to a
// client. Tokens are rotated on every refresh and can be revoked on logout.
type RefreshToken struct {
	gorm.Model
	UserID    uint      `gorm:"not null;index" json:"-"`
	TokenHash string    `gorm:"size:64;uniqueIndex;not null" json:"-"`
	ExpiresAt time.Time `gorm:"not null;index" json:"-"`
}
