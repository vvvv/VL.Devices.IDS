# VL.Devices.IDS
Support for uEye and uEye+ industrial cameras by [IDS Imaging](https://ids-imaging.com).

For use with vvvv, the visual live-programming environment for .NET: http://vvvv.org

## Getting started
- For uEye+ cameras install [IDS peak](https://en.ids-imaging.com/ids-peak.html) (Tested with version > 26.06.2)
- For uEye cameras:
  - Run a "Custom" installation of IDS peak and activate "uEye Transport Layer"
  - Also install the [IDS Softwaresuite](https://de.ids-imaging.com/ids-software-suite.html) version >= 4.97
- Install as [described here](https://thegraybook.vvvv.org/reference/hde/managing-nugets.html) via commandline:

    `nuget install VL.Devices.IDS`

- Usage examples and more information are included in the pack and can be found via the [Help Browser](https://thegraybook.vvvv.org/reference/hde/findinghelp.html)

## Changing properties while the camera is running
- `ConfigProperty` is applied when the acquisition starts. Changing its value restarts the whole acquisition (device is closed and opened again).
- `SetProperty` writes a value to the running camera without a restart, e.g. `ExposureTime` or `Gain`. The value is written on every change and again after each restart of the acquisition. Numeric values are clamped to the current range (e.g. the maximum `ExposureTime` depends on the frame rate). Properties which are locked during acquisition (`Width`, `Height`, `PixelFormat`, ...) cannot be changed this way.
- `GetProperty` returns a snapshot taken at acquisition start. Enable its optional `Live` pin to read the current value from the running camera.
- Frames already queued in the driver may still carry the previous value. Use the `Applied` output of `SetProperty` to skip a few frames after a change if needed.

## Contributing
- Report issues on [the vvvv forum](https://discourse.vvvv.org/c/vvvv-gamma/28)
- For custom development requests, please [get in touch](mailto:devvvvs@vvvv.org)
- When making a pull-request, please make sure to read the general [guidelines on contributing to vvvv libraries](https://thegraybook.vvvv.org/reference/extending/contributing.html)

## Credits
Based on the [IDS peak SDK](https://de.ids-imaging.com/ids-peak.html).

## Sponsoring
Development of this library was partially sponsored by:  
* [Refik Anadol Studio](https://refikanadolstudio.com)
