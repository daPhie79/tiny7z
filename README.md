# tiny7z
tiny7z is a native C# SevenZip 7zip .7z file format archive reader/writer

---

## Objective

- Provide a native code only C# library that supports writing to .7zip archives.
- Goal achieved.

## Features

- Read .7zip archives, with uncompressed or compressed headers (as long as the software used LZMA codec).
- Write to .7zip archives, using LZMA codec, in a single block, or one block per file.

## Current limitations

*They are plenty unfortunately, but this library is still a huge step forward for .7z support in native C#*

- Only support LZMA codec
- No filter support yet
- Limited set of methods (mainly ExtractAll and CompressAll)
- De/compression is slower than native 7z.dll (however this is due to pure C# implementation of LZMA SDK)

---

## Links

- [https://www.7-zip.org/sdk.html] (LZMA SDK Development Kit) by Igor Pavlov

---

**2018 (c) princess_daphie**
