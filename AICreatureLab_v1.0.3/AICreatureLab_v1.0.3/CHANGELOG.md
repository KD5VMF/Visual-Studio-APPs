# Changelog

## v1.0.3
- fixed intermittent `Collection was modified` crash during parallel creature sensing
- switched worker sensing to safer snapshot reads for food, hazards, and creature positions
- added render/history snapshot reads to reduce UI enumeration risk
- added crash-log writing and automatic pause-on-error behavior
- updated visible in-app version strings to `1.0.3`

## v1.0.2
- added parallel creature update path using up to the workstation's logical processor count
- increased default world population/resource counts for heavier workstation-class loads
- added frame timing and worker-thread info to the UI
- fixed hardcoded old version text in the HUD and save-file naming

## v1.0.1
- fixed ambiguous Timer reference by binding to `System.Windows.Forms.Timer`

## v1.0.0
- initial C# Windows Forms release
