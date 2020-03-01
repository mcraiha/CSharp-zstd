# CSharp-zstd
C# managed implementation of [zstd](https://facebook.github.io/zstd/) (Zstandard) compression algorithm. Currently only the decompression part.

Basically [the RFC](https://tools.ietf.org/html/rfc8478) is too hard to understand, so I just ported existing Zstd implementation from Java based [aircompressor](https://github.com/airlift/aircompressor) to C#. Yes, the porting experience was horrible, because Java does not have unsigned integer types.

## Why
Because I needed the decompression for another project.

## How to use
Coming up later

## How do I build this
### Requirements
Dotnet core 2.0 environment

### Build .dll
Move to **src** folder and run
```bash
dotnet build
```

### Build nuget
TBA

## Testing
### Requirements 
* nunit
* NUnit3TestAdapter
* Microsoft.NET.Test.Sdk

All requirements are restored when you run
```bash
dotnet restore
```

### Run tests
Just call
```bash
dotnet test
```

## What is in
* Basic decompress method

## What is missing
* Compress functionality

## License
All code is released under *Apache License*, see [License](LICENSE). It is same license as [aircompressor](https://github.com/airlift/aircompressor) has.