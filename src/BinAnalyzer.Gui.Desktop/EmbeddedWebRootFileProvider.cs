using System.Reflection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;

namespace BinAnalyzer.Gui.Desktop;

/// <summary>
/// 物理 wwwroot を持たずに、埋め込みリソースから静的ファイルを配信する。
/// <c>index.html</c> は本アセンブリ、<c>_content/BinAnalyzer.Gui/*</c> は Gui アセンブリの埋め込みリソースを参照する。
/// CLI を単一ファイル publish しても動くようにするための仕組み。
/// </summary>
public sealed class EmbeddedWebRootFileProvider : IFileProvider
{
    private readonly IReadOnlyList<Assembly> _assemblies;

    public EmbeddedWebRootFileProvider()
        : this([typeof(EmbeddedWebRootFileProvider).Assembly, typeof(Components.GuiShell).Assembly])
    {
    }

    public EmbeddedWebRootFileProvider(IReadOnlyList<Assembly> assemblies)
    {
        _assemblies = assemblies;
    }

    public IFileInfo GetFileInfo(string subpath)
    {
        var name = Normalize(subpath);
        foreach (var asm in _assemblies)
        {
            if (asm.GetManifestResourceInfo(name) is null)
                continue;
            return new EmbeddedFile(asm, name);
        }
        return new NotFoundFileInfo(subpath);
    }

    public IDirectoryContents GetDirectoryContents(string subpath) => NotFoundDirectoryContents.Singleton;

    public IChangeToken Watch(string filter) => NullChangeToken.Singleton;

    internal static string Normalize(string subpath)
    {
        var p = subpath.Replace('\\', '/').TrimStart('/');
        return p.Length == 0 ? "index.html" : p;
    }

    private sealed class EmbeddedFile : IFileInfo
    {
        private readonly Assembly _assembly;
        private readonly string _resourceName;
        private readonly long _length;

        public EmbeddedFile(Assembly assembly, string resourceName)
        {
            _assembly = assembly;
            _resourceName = resourceName;
            using var s = assembly.GetManifestResourceStream(resourceName)!;
            _length = s.Length;
        }

        public bool Exists => true;
        public long Length => _length;
        public string? PhysicalPath => null;
        public string Name => Path.GetFileName(_resourceName);
        public DateTimeOffset LastModified => DateTimeOffset.MinValue;
        public bool IsDirectory => false;
        public Stream CreateReadStream() => _assembly.GetManifestResourceStream(_resourceName)!;
    }
}
