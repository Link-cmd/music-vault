# Third-party components

## NAudio 1.10.0

Copyright Mark Heath and contributors. Licensed under the Microsoft Public License; see NAudio-LICENSE.txt. The build embeds the net35 assembly unchanged and loads it from the executable.

- Project/source: https://github.com/naudio/NAudio/tree/v1.10.0
- Package: https://www.nuget.org/packages/NAudio/1.10.0
- Package SHA-256: `e4a80eed41dad794695f9c114432de4667d20bdfa80825aa94d159708cf29217`

## FFmpeg 7.1, gyan.dev essentials build

Copyright 2000–2024 the FFmpeg developers. This build is licensed under GPL version 3 or later; see FFmpeg-LICENSE.txt. It is bundled unchanged and run as a separate local process when the Windows decoder fails or returns no audio.

The setup script obtains this build from the Windows x86-64 wheel of imageio-ffmpeg 0.6.0. The wheel's own license and metadata remain in `.deps/decoder-package`. Binary filename: `ffmpeg-win-x86_64-v7.1.exe`.

- Project: https://ffmpeg.org/
- Source version: https://github.com/FFmpeg/FFmpeg/tree/n7.1
- Build provider and source/build information: https://www.gyan.dev/ffmpeg/builds/
- Package: https://pypi.org/project/imageio-ffmpeg/0.6.0/
- Wheel SHA-256: `02fa47c83703c37df6bfe4896aab339013f62bf02c5ebf2dce6da56af04ffc0a`
- Executable SHA-256: `2ce797a0f88d7f067180338fb227f7b1928ea727bd9a4d7a1d022f7c52af71a3`

The repository stores no dependency binaries. If you distribute a compiled player, you also distribute these embedded components. Retain their notices and satisfy their licenses, including GPL requirements for complete corresponding source and applicable source-access information for the exact FFmpeg build and its dependencies. Merely linking to an arbitrary upstream version is not a substitute for the applicable corresponding-source obligation. Run the decoder with `-L` and `-buildconf` to inspect its license and configuration. Application source in this repository does not include the corresponding source for all third-party binaries.
