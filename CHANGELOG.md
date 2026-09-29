# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [1.1.0] - 2026-09-29

### Added

- Take All also takes the prey in the storage box of an Auto Trap Cage, with the game's own pickup of the "Collect All" button in the trap window. If the bag has room for only a part of the prey, the rest stays in the storage box and the route stops at that cage.
- A Stopped Auto Trap Cage (its storage box is full) has a red border and a red dot in the trap grid.
- A trap with more than one prey shows the game's count (for example ×4) as a small badge in the corner of its cell.

### Removed

- Support for game versions before 1.1.18153 (the Autumn Update). Trapline 1.1.0 needs Survival Log 1.1.18153 or later.

## [1.0.1] - 2026-09-27

### Changed

- Trapline sends less repeated data to the HUD when you place or remove traps. The Take All button and trap grid look and work as before.

## [1.0.0] - 2026-09-24

### Added

- "Take All" button in the header of the HUD trap list, shown when at least one trap has prey. A click queues the game's normal pickup action for each trap with prey, floor by floor, with the nearest trap next.
- If the bag fills up during the route, the character stops at that trap and the rest of the route leaves the action queue. Actions you queued yourself stay.
- The open trap list is a compact grid: one line for each floor, with one cell for each trap slot you can use now. A cell shows the trap icon, or it is empty for a free slot. A trap with prey has a gold border and a gold dot.
