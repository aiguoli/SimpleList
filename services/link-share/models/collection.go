package models

import "gorm.io/gorm"

type Collection struct {
	gorm.Model
	UserID  uint `gorm:"not null;uniqueIndex:idx_user_share;index" json:"user_id"`
	ShareID uint `gorm:"not null;uniqueIndex:idx_user_share;index" json:"share_id"`
}
