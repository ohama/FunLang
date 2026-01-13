#!/bin/bash
#
# FunLang Version Upgrade Script
#
# Usage:
#   ./upgrade_version.sh [major|minor|patch] [--push]
#
# Examples:
#   ./upgrade_version.sh minor --push   # 0.1.0 -> 0.2.0, push to remote
#   ./upgrade_version.sh patch          # 0.1.0 -> 0.1.1, no push
#   ./upgrade_version.sh major          # 0.1.0 -> 1.0.0, no push
#

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Configuration
VERSION_FILE="VERSION"
CHANGELOG_FILE="CHANGELOG.md"

# Parse arguments
BUMP_TYPE="${1:-minor}"
DO_PUSH=false

for arg in "$@"; do
    case $arg in
        --push)
            DO_PUSH=true
            ;;
        major|minor|patch)
            BUMP_TYPE="$arg"
            ;;
    esac
done

# Validate bump type
if [[ ! "$BUMP_TYPE" =~ ^(major|minor|patch)$ ]]; then
    echo -e "${RED}Error: Invalid bump type '$BUMP_TYPE'. Use major, minor, or patch.${NC}"
    exit 1
fi

# Check if VERSION file exists
if [[ ! -f "$VERSION_FILE" ]]; then
    echo -e "${RED}Error: $VERSION_FILE not found${NC}"
    exit 1
fi

# Read current version
CURRENT_VERSION=$(cat "$VERSION_FILE" | tr -d '[:space:]')
echo -e "${BLUE}Current version: ${YELLOW}$CURRENT_VERSION${NC}"

# Parse version components
IFS='.' read -r MAJOR MINOR PATCH <<< "$CURRENT_VERSION"

# Bump version
case $BUMP_TYPE in
    major)
        MAJOR=$((MAJOR + 1))
        MINOR=0
        PATCH=0
        ;;
    minor)
        MINOR=$((MINOR + 1))
        PATCH=0
        ;;
    patch)
        PATCH=$((PATCH + 1))
        ;;
esac

NEW_VERSION="$MAJOR.$MINOR.$PATCH"
echo -e "${BLUE}New version: ${GREEN}$NEW_VERSION${NC}"

# Get today's date
TODAY=$(date +%Y-%m-%d)

# Get commits since last tag or initial commit
LAST_TAG=$(git describe --tags --abbrev=0 2>/dev/null || echo "")
if [[ -z "$LAST_TAG" ]]; then
    # No tags yet, get all commits
    COMMITS=$(git log --oneline --no-decorate)
else
    COMMITS=$(git log --oneline --no-decorate "$LAST_TAG"..HEAD)
fi

# Generate changelog entry
echo -e "${BLUE}Generating changelog entry...${NC}"

# Categorize commits
ADDED=""
CHANGED=""
FIXED=""
REMOVED=""

while IFS= read -r line; do
    if [[ -z "$line" ]]; then
        continue
    fi

    # Extract commit message (remove hash)
    MSG=$(echo "$line" | sed 's/^[a-f0-9]* //')

    # Categorize based on keywords
    case "$MSG" in
        Add*|add*|Implement*|implement*|Create*|create*|Initial*|Support*)
            ADDED="$ADDED\n- $MSG"
            ;;
        Remove*|remove*|Delete*|delete*)
            REMOVED="$REMOVED\n- $MSG"
            ;;
        Fix*|fix*|Resolve*|resolve*|Bug*|bug*)
            FIXED="$FIXED\n- $MSG"
            ;;
        *)
            CHANGED="$CHANGED\n- $MSG"
            ;;
    esac
done <<< "$COMMITS"

# Build changelog entry
CHANGELOG_ENTRY="## [$NEW_VERSION] - $TODAY"

if [[ -n "$ADDED" ]]; then
    CHANGELOG_ENTRY="$CHANGELOG_ENTRY\n\n### Added$ADDED"
fi

if [[ -n "$CHANGED" ]]; then
    CHANGELOG_ENTRY="$CHANGELOG_ENTRY\n\n### Changed$CHANGED"
fi

if [[ -n "$FIXED" ]]; then
    CHANGELOG_ENTRY="$CHANGELOG_ENTRY\n\n### Fixed$FIXED"
fi

if [[ -n "$REMOVED" ]]; then
    CHANGELOG_ENTRY="$CHANGELOG_ENTRY\n\n### Removed$REMOVED"
fi

echo -e "${BLUE}Changelog entry:${NC}"
echo -e "$CHANGELOG_ENTRY"
echo ""

# Confirm before proceeding
read -p "Proceed with version upgrade? (y/N) " -n 1 -r
echo
if [[ ! $REPLY =~ ^[Yy]$ ]]; then
    echo -e "${YELLOW}Aborted.${NC}"
    exit 0
fi

# Update VERSION file
echo "$NEW_VERSION" > "$VERSION_FILE"
echo -e "${GREEN}Updated $VERSION_FILE${NC}"

# Update CHANGELOG.md
# Insert new entry after "## [Unreleased]" line
if [[ -f "$CHANGELOG_FILE" ]]; then
    # Create temp file with new changelog entry
    awk -v entry="$CHANGELOG_ENTRY" '
    /^## \[Unreleased\]/ {
        print $0
        print ""
        # Print the entry (handle \n)
        gsub(/\\n/, "\n", entry)
        print entry
        next
    }
    { print }
    ' "$CHANGELOG_FILE" > "${CHANGELOG_FILE}.tmp"
    mv "${CHANGELOG_FILE}.tmp" "$CHANGELOG_FILE"
    echo -e "${GREEN}Updated $CHANGELOG_FILE${NC}"
else
    echo -e "${YELLOW}Warning: $CHANGELOG_FILE not found, skipping...${NC}"
fi

# Git operations
echo -e "${BLUE}Committing changes...${NC}"
git add "$VERSION_FILE" "$CHANGELOG_FILE"
git commit -m "Release v$NEW_VERSION"

echo -e "${BLUE}Creating tag v$NEW_VERSION...${NC}"
git tag -a "v$NEW_VERSION" -m "Release v$NEW_VERSION"

echo -e "${GREEN}Version $NEW_VERSION released locally!${NC}"

# Push if requested
if $DO_PUSH; then
    echo -e "${BLUE}Pushing to remote...${NC}"
    git push origin main
    git push origin "v$NEW_VERSION"
    echo -e "${GREEN}Pushed to remote!${NC}"
else
    echo -e "${YELLOW}To push changes, run:${NC}"
    echo "  git push origin main"
    echo "  git push origin v$NEW_VERSION"
fi

echo -e "${GREEN}Done!${NC}"
