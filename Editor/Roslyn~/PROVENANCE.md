# Editor semantic compiler dependency

The two reflection-loaded assemblies are the `lib/netstandard2.0` products of
Microsoft.CodeAnalysis.Common4.10.0 and Microsoft.CodeAnalysis.CSharp4.10.0,
published by Microsoft on NuGet from Roslyn commit
`2b7d172669b2f7e55803b55f317cfcc2d4279d76`.

- [Common package](https://www.nuget.org/packages/Microsoft.CodeAnalysis.Common/4.10.0)
- [CSharp package](https://www.nuget.org/packages/Microsoft.CodeAnalysis.CSharp/4.10.0)

Both are MIT licensed; LICENSE.txt and ThirdPartyNotices.rtf accompany them.
Unity does not import this tilde directory. The compiler owner loads only this
pair, through Unity's reload-safe assembly API, once per Editor domain.
The pair occupies12,896,280 bytes; there is no additional scan or retained cache.

SHA256:

```text
D39519D1CA3528859351AFA5CFB79540090F96E166282D4DF2333E96FC90C9CD  Microsoft.CodeAnalysis.dll
B7D6B20069C5371206B0832A7E6BEE36F29C09CF096D8C2DED17E15C4C1376D2  Microsoft.CodeAnalysis.CSharp.dll
```
