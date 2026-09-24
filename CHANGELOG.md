# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [1.0.0] - 2026-09-24

### Added

- "Take All" button in the header of the HUD trap list, shown when at least one trap has prey. A click queues the game's normal pickup action for each trap with prey, floor by floor, with the nearest trap next.
- If the bag fills up during the route, the character stops at that trap and the rest of the route leaves the action queue. Actions you queued yourself stay.
- The open trap list is a compact grid: one line for each floor, with one cell for each trap slot you can use now. A cell shows the trap icon, or it is empty for a free slot. A trap with prey has a gold border and a gold dot.
