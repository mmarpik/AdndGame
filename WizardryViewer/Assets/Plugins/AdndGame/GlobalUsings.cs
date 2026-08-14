// Unity-only plumbing: mirrors the SDK-generated GlobalUsings.g.cs that Adnd.Core.csproj
// and Adnd.Data.csproj get for free from <ImplicitUsings>enable</ImplicitUsings>. Unity's
// compiler has no equivalent MSBuild step, so several synced files (SpellRepository.cs,
// TreasureTableRepository.cs, etc.) would otherwise fail to resolve Directory/File/List<T>/
// LINQ extension methods they never explicitly import. Not synced from either repo - it has
// no upstream counterpart to drift from.
global using System;
global using System.Collections.Generic;
global using System.IO;
global using System.Linq;
global using System.Net.Http;
global using System.Threading;
global using System.Threading.Tasks;
