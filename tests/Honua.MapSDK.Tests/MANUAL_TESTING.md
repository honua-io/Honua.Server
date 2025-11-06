# Manual Testing Guide for Honua.MapSDK

This guide outlines manual testing procedures for the Honua.MapSDK library. Use this checklist before releases and when automated tests don't cover specific scenarios.

## Table of Contents

- [Browser Compatibility Testing](#browser-compatibility-testing)
- [Component Testing Checklists](#component-testing-checklists)
- [Accessibility Testing](#accessibility-testing)
- [Performance Testing](#performance-testing)
- [Mobile & Responsive Testing](#mobile--responsive-testing)
- [Integration Scenarios](#integration-scenarios)

## Browser Compatibility Testing

Test the MapSDK in the following browsers:

### Desktop Browsers

- [ ] **Chrome** (latest version)
  - [ ] All components render correctly
  - [ ] Map interactions work smoothly
  - [ ] No console errors

- [ ] **Firefox** (latest version)
  - [ ] All components render correctly
  - [ ] Map interactions work smoothly
  - [ ] No console errors

- [ ] **Safari** (latest version, macOS)
  - [ ] All components render correctly
  - [ ] Map interactions work smoothly
  - [ ] WebGL support works

- [ ] **Edge** (latest version)
  - [ ] All components render correctly
  - [ ] Map interactions work smoothly
  - [ ] No console errors

### Mobile Browsers

- [ ] **Safari** (iOS latest)
  - [ ] Touch interactions work
  - [ ] Responsive layout adapts
  - [ ] Performance acceptable

- [ ] **Chrome** (Android latest)
  - [ ] Touch interactions work
  - [ ] Responsive layout adapts
  - [ ] Performance acceptable

## Component Testing Checklists

### HonuaMap Component

#### Basic Functionality
- [ ] Map loads with default settings
- [ ] Map loads with custom center and zoom
- [ ] Map applies custom style URL
- [ ] Map bounds restrictions work
- [ ] Min/max zoom constraints work

#### Interactions
- [ ] Pan (drag) works smoothly
- [ ] Zoom in/out (scroll wheel)
- [ ] Zoom in/out (double-click)
- [ ] Zoom in/out (pinch on mobile)
- [ ] Rotate (Ctrl+drag or two-finger on mobile)
- [ ] Pitch (Ctrl+drag up/down)

#### Features
- [ ] FlyTo animation works
- [ ] FitBounds works correctly
- [ ] Layer visibility toggle works
- [ ] Layer opacity adjustment works
- [ ] Feature click events fire
- [ ] Feature hover events fire
- [ ] Popup display works

#### Performance
- [ ] Map loads within 2 seconds
- [ ] 60 FPS during pan/zoom
- [ ] No memory leaks on repeated load/unload
- [ ] Large datasets render smoothly

---

### HonuaDataGrid Component

#### Basic Functionality
- [ ] Grid renders with empty data
- [ ] Grid renders with sample data
- [ ] Columns auto-generate correctly
- [ ] Custom columns display

#### Filtering & Sorting
- [ ] Search/filter box works
- [ ] Column filters work
- [ ] Sorting by column (ascending)
- [ ] Sorting by column (descending)
- [ ] Multi-column sorting

#### Selection
- [ ] Single row selection
- [ ] Multi-row selection
- [ ] Row click highlights on map
- [ ] Row selection syncs with map

#### Pagination
- [ ] Page navigation works
- [ ] Page size selector works
- [ ] Total count displays correctly

#### Export
- [ ] Export to JSON
- [ ] Export to CSV
- [ ] Export to GeoJSON
- [ ] Downloaded files are valid

#### Map Sync
- [ ] Auto-sync with map extent
- [ ] Manual sync toggle works
- [ ] Filtering by map bounds works

---

### HonuaChart Component

#### Chart Types
- [ ] Bar chart renders
- [ ] Line chart renders
- [ ] Pie chart renders
- [ ] Scatter plot renders
- [ ] Histogram renders
- [ ] Area chart renders

#### Data Aggregation
- [ ] Count aggregation
- [ ] Sum aggregation
- [ ] Average aggregation
- [ ] Min/Max aggregation

#### Interactions
- [ ] Click-to-filter works
- [ ] Hover tooltip displays
- [ ] Chart updates with data changes
- [ ] Legend toggle works

#### Theming
- [ ] Light theme applies
- [ ] Dark theme applies
- [ ] Custom colors work

#### Export
- [ ] Export to PNG
- [ ] Export to SVG
- [ ] Export data to CSV

---

### HonuaLegend Component

#### Basic Functionality
- [ ] Legend displays all layers
- [ ] Layer names display correctly
- [ ] Layer icons/symbols display

#### Interactions
- [ ] Visibility toggle works
- [ ] Opacity slider works
- [ ] Layer reordering works
- [ ] Expand/collapse groups

#### Synchronization
- [ ] Legend updates when layers added
- [ ] Legend updates when layers removed
- [ ] Legend reflects current visibility
- [ ] Legend reflects current opacity

---

### HonuaFilterPanel Component

#### Spatial Filters
- [ ] Bounding box filter
- [ ] Draw polygon filter
- [ ] Draw circle filter
- [ ] Within distance filter

#### Attribute Filters
- [ ] Equals filter
- [ ] Greater than / Less than
- [ ] Contains filter
- [ ] In list filter
- [ ] Is null / Is not null

#### Temporal Filters
- [ ] Date range picker
- [ ] Before/After filters
- [ ] Last N days/weeks/months
- [ ] Custom time range

#### Filter Management
- [ ] Add new filter
- [ ] Remove filter
- [ ] Clear all filters
- [ ] Active filters display
- [ ] Filter expressions generate correctly

#### Synchronization
- [ ] Filters apply to map
- [ ] Filters apply to grid
- [ ] Filters apply to chart
- [ ] Multiple filters combine correctly

---

### HonuaSearch Component

#### Search Functionality
- [ ] Search by address
- [ ] Search by place name
- [ ] Search by coordinates
- [ ] Autocomplete suggestions

#### Providers
- [ ] Nominatim provider works
- [ ] Mapbox provider works
- [ ] Custom provider integration

#### Results
- [ ] Results display correctly
- [ ] Result selection flies to location
- [ ] Result highlights on map
- [ ] Recent searches saved
- [ ] Recent searches display

#### Geolocation
- [ ] "Use my location" button
- [ ] Geolocation permission prompt
- [ ] Current location marker

---

### HonuaTimeline Component

#### Basic Functionality
- [ ] Timeline renders
- [ ] Time range displays
- [ ] Current time indicator
- [ ] Time labels format correctly

#### Playback Controls
- [ ] Play button starts animation
- [ ] Pause button stops animation
- [ ] Stop button resets to start
- [ ] Step forward
- [ ] Step backward

#### Configuration
- [ ] Custom step size works
- [ ] Playback speed adjustment
- [ ] Loop mode works
- [ ] Time range customization

#### Synchronization
- [ ] Map updates with time changes
- [ ] Grid filters by current time
- [ ] Chart updates with time
- [ ] Multiple components stay in sync

---

## Accessibility Testing

### Keyboard Navigation
- [ ] Tab through all interactive elements
- [ ] Enter/Space activate buttons
- [ ] Arrow keys navigate lists/grids
- [ ] Escape closes modals/popups
- [ ] Focus visible on all elements

### Screen Reader Testing
Test with NVDA (Windows) or VoiceOver (macOS):

- [ ] All buttons have labels
- [ ] Form fields have labels
- [ ] Images have alt text
- [ ] Map announces state changes
- [ ] Grid announces row selection
- [ ] Filters announce when applied

### Color Contrast
- [ ] Text meets WCAG AA (4.5:1)
- [ ] Interactive elements visible
- [ ] Focus indicators clearly visible
- [ ] Charts use accessible colors
- [ ] Colorblind-friendly palettes

### ARIA Attributes
- [ ] Roles assigned correctly
- [ ] Live regions announce changes
- [ ] Expanded/collapsed states
- [ ] Selected states indicated

---

## Performance Testing

### Load Time
- [ ] Initial page load < 3 seconds
- [ ] Map initialization < 2 seconds
- [ ] Component lazy loading works

### Large Datasets
Test with 10,000+ features:

- [ ] Map renders without lag
- [ ] Grid paginates correctly
- [ ] Chart aggregates efficiently
- [ ] Filtering remains responsive
- [ ] Memory usage stays reasonable

### Network Conditions
Test with Chrome DevTools throttling:

- [ ] Works on Fast 3G
- [ ] Graceful degradation on Slow 3G
- [ ] Offline detection works
- [ ] Loading indicators display

### Memory Leaks
- [ ] Repeated component mount/unmount
- [ ] No memory growth in Chrome DevTools
- [ ] Event listeners cleaned up
- [ ] ComponentBus subscriptions removed

---

## Mobile & Responsive Testing

### Viewport Sizes
- [ ] **Desktop** (1920x1080)
- [ ] **Laptop** (1366x768)
- [ ] **Tablet** (768x1024)
- [ ] **Mobile** (375x667)
- [ ] **Mobile Landscape** (667x375)

### Touch Interactions
- [ ] Tap to select
- [ ] Long press for context menu
- [ ] Pinch to zoom
- [ ] Two-finger rotate
- [ ] Swipe to pan

### Responsive Layout
- [ ] Grid columns stack on mobile
- [ ] Charts resize appropriately
- [ ] Controls accessible on small screens
- [ ] Text remains readable
- [ ] No horizontal scrolling

---

## Integration Scenarios

### Map + DataGrid Sync
1. [ ] Load data in both components
2. [ ] Click feature on map → grid row highlights
3. [ ] Click grid row → map feature highlights
4. [ ] Pan map → grid filters by extent
5. [ ] Apply filter → both components update

### Map + Chart + Timeline
1. [ ] Load temporal data
2. [ ] Chart displays aggregated data
3. [ ] Start timeline playback
4. [ ] Map and chart update in sync
5. [ ] Pause/resume works correctly

### Complete Workflow
1. [ ] Load map with multiple layers
2. [ ] Add data grid with auto-sync
3. [ ] Add chart showing statistics
4. [ ] Add legend for layer control
5. [ ] Add filter panel
6. [ ] Apply spatial filter → all update
7. [ ] Apply attribute filter → all update
8. [ ] Export data from grid
9. [ ] Export image from map
10. [ ] Save configuration

### Multi-Map Scenarios
1. [ ] Two maps on same page
2. [ ] Maps remain independent
3. [ ] Sync buttons work when enabled
4. [ ] ComponentBus routes correctly
5. [ ] No cross-contamination

---

## Test Data Scenarios

### Empty States
- [ ] Map with no layers
- [ ] Grid with no data
- [ ] Chart with no data
- [ ] Legend with no layers
- [ ] Search with no results

### Error States
- [ ] Invalid GeoJSON
- [ ] Failed HTTP request
- [ ] Invalid map style URL
- [ ] Network timeout
- [ ] Invalid filter expression

### Edge Cases
- [ ] Very long layer names
- [ ] Special characters in data
- [ ] NULL/undefined values
- [ ] Date parsing edge cases
- [ ] Large coordinate values
- [ ] Crossing date line
- [ ] Polar regions

---

## Testing Tools

### Browser DevTools
- **Console**: Check for errors/warnings
- **Network**: Monitor requests, timing
- **Performance**: Profile frame rate
- **Memory**: Check for leaks
- **Lighthouse**: Run audits

### Testing Extensions
- **axe DevTools** - Accessibility testing
- **WAVE** - Accessibility evaluation
- **ColorZilla** - Color contrast checking

### Mobile Testing
- **Chrome Remote Debugging** - Test on real Android devices
- **Safari Web Inspector** - Test on real iOS devices
- **BrowserStack** - Cross-browser/device testing

---

## Bug Reporting Template

When filing bugs from manual testing:

```
**Component**: [Map/Grid/Chart/etc.]
**Browser**: [Chrome 120 / Firefox 121 / etc.]
**OS**: [Windows 11 / macOS 14 / etc.]
**Severity**: [Critical/High/Medium/Low]

**Steps to Reproduce**:
1.
2.
3.

**Expected Behavior**:


**Actual Behavior**:


**Screenshots/Videos**:


**Console Errors**:


**Additional Context**:

```

---

## Pre-Release Checklist

Before each release, verify:

- [ ] All automated tests pass
- [ ] All critical manual tests pass
- [ ] Browser compatibility verified
- [ ] Accessibility audit complete
- [ ] Performance benchmarks met
- [ ] Documentation updated
- [ ] Examples work correctly
- [ ] No console errors/warnings
- [ ] Code coverage > 80%

---

## Notes

- Automated tests cover most scenarios, but manual testing catches UI/UX issues
- Focus manual testing on visual, interactive, and cross-browser scenarios
- Document any new issues found during manual testing
- Update automated tests to cover new manual test scenarios when possible
