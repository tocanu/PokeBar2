using System;
using System.IO;
using System.Linq;
using Pokebar.Core.Models;
using Xunit;

namespace Pokebar.Tests;

public class SpriteDirectoryHelperTests
{
    [Fact]
    public void EnumerateSpriteFolders_IncludesRootWhenNo0000Subfolder()
    {
        var root = CreateTempRoot();
        try
        {
            var dexPath = Path.Combine(root, "0001");
            Directory.CreateDirectory(dexPath);
            File.WriteAllBytes(Path.Combine(dexPath, "Walk-Anim.png"), Array.Empty<byte>());
            Directory.CreateDirectory(Path.Combine(dexPath, "0006"));

            var variants = SpriteDirectoryHelper.EnumerateSpriteFolders(root).ToList();

            Assert.Contains(variants, v => v.DexNumber == 1 && v.FormId == "0000" && v.Path == dexPath);
            Assert.Contains(variants, v => v.DexNumber == 1 && v.FormId == "0006" && v.Path == Path.Combine(dexPath, "0006"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void EnumerateSpriteFolders_ExcludesRootWhen0000SubfolderExists()
    {
        var root = CreateTempRoot();
        try
        {
            var dexPath = Path.Combine(root, "0001");
            Directory.CreateDirectory(dexPath);
            File.WriteAllBytes(Path.Combine(dexPath, "Walk-Anim.png"), Array.Empty<byte>());
            Directory.CreateDirectory(Path.Combine(dexPath, "0000"));

            var variants = SpriteDirectoryHelper.EnumerateSpriteFolders(root).ToList();

            Assert.DoesNotContain(variants, v => v.DexNumber == 1 && v.FormId == "0000" && v.Path == dexPath);
            Assert.Contains(variants, v => v.DexNumber == 1 && v.FormId == "0000" && v.Path == Path.Combine(dexPath, "0000"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateTempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "pokebar_tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }
}
