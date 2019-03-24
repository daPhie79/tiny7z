# tiny7z
tiny7z is a native C# SevenZip 7zip .7z file format archive reader/writer

---

## Objective

- Provide a native code only C# library that supports writing to .7zip archives.

## Features

- Read .7zip archives, with uncompressed or compressed headers.
- Write to .7zip archives, using LZMA codec, in a single block (solid), or one block per file.
- Support LZMA, LZMA2, PPMd decoders.
- Support AES, BCJ and BCJ2 decoder filters.

## Releases

- v0.1 - First release, unofficial, lots of features missing, incomplete test app
- v0.2 - First official release, command-line test app

## Current limitations

*They are plenty unfortunately, but this library is still a huge step forward for compact .7z support in native C#*

- LZMA Compression is slower than native 7z.dll (due to the pure C# implementation of LZMA SDK).
- LZMA Decompression is slower than native 7z.dll, and also than Igor Pavlov's official C# LZMA decoder, because while Tobias Käs' version of the decompressor is slower than Igor Pavlov's, his encoder is faster, but since the goal of this library is to be compact and to complete SharpCompress by providing a native C# encoder, I have kept code simpler by only implementing one compression library and priorizing compression speed.
- Probably other details I haven't thought of.

---

## Links

- [https://www.7-zip.org/sdk.html] (LZMA SDK Development Kit) by Igor Pavlov
- [https://github.com/adamhathcock/sharpcompress] (SharpCompress) by Adam Hathcock
- [https://github.com/weltkante/managed-lzma] (C# implementation of LZMA and 7zip) by Tobias Käs

---

**2019 (c) princess_daphie**
