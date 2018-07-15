# tiny7z
tiny7z is a native C# SevenZip 7zip .7z file format archive reader/writer

---

## Objective

- Provide a native code only C# library that supports writing to .7zip archives.

## Features

- Read .7zip archives, with uncompressed or compressed headers.
- Write to .7zip archives, using LZMA codec, in a single block (solid), or one block per file.
- Support LZMA, LZMA2, PPMd decoders.
- Support BCJ and BCJ2 decoder filters.

## Current limitations

*They are plenty unfortunately, but this library is still a huge step forward for .7z support in native C#*

- De/compression are slower than native 7z.dll (however this is due to pure C# implementation of LZMA SDK).
- AES decoder is in place, but no support for password yet.

---

## Links

- [https://www.7-zip.org/sdk.html] (LZMA SDK Development Kit) by Igor Pavlov
- [https://github.com/adamhathcock/sharpcompress] (Fully managed C# library) SharpCompress

---

**2018 (c) princess_daphie**
