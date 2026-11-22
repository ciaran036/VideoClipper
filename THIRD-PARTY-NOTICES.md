# Third-party notices

## FFmpeg

Video Clipper's installer includes an unmodified copy of **FFmpeg**, which does
the actual cutting. Video Clipper runs it as a separate program
(`ffmpeg\ffmpeg.exe`); it is not linked into Video Clipper.

- Project: <https://ffmpeg.org> - source code: <https://github.com/FFmpeg/FFmpeg>
- Licence: GNU Lesser General Public License v2.1 or later (LGPL). The licence
  text is installed next to FFmpeg in the `ffmpeg` folder (`LICENSE.txt`).
- Binaries: the official **LGPL, shared** Windows builds from
  <https://github.com/BtbN/FFmpeg-Builds> (the exact build used is pinned in
  `.github/workflows/release.yml`). Those builds contain no GPL components.
  The build scripts and configuration are in that repository.
- You may replace FFmpeg with your own build: put an `ffmpeg.exe` in the
  `ffmpeg` folder inside the install directory.

FFmpeg is a trademark of Fabrice Bellard, originator of the FFmpeg project.
