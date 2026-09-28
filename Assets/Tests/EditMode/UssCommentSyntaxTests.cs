using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace TerrariumDays.Tests
{
    public sealed class UssCommentSyntaxTests
    {
        [Test]
        public void EveryUssFileHasNoStrayOrUnterminatedComments()
        {
            var uiDirectory = Path.Combine(Application.dataPath, "UI");
            foreach (var path in Directory.GetFiles(uiDirectory, "*.uss", SearchOption.AllDirectories))
            {
                AssertBalancedComments(path, File.ReadAllText(path));
            }
        }

        private static void AssertBalancedComments(string path, string text)
        {
            var inComment = false;
            var line = 1;

            for (var i = 0; i < text.Length; i++)
            {
                if (text[i] == '\n')
                {
                    line++;
                }

                if (!inComment && i + 1 < text.Length && text[i] == '/' && text[i + 1] == '*')
                {
                    inComment = true;
                    i++;
                    continue;
                }

                if (i + 1 < text.Length && text[i] == '*' && text[i + 1] == '/')
                {
                    Assert.That(inComment, Is.True,
                        $"{path}:{line} has a stray '*/' outside any comment block. " +
                        "A comment whose own text contains '*/' closes early and corrupts everything " +
                        "after it (the bug fixed in commit 3a2be90) — write it without the slash-star pair, " +
                        "e.g. 'the .shop- and .ledger- rows' instead of '.shop-*/.ledger-*'.");
                    inComment = false;
                    i++;
                    continue;
                }
            }

            Assert.That(inComment, Is.False, $"{path} has an unterminated '/*' comment.");
        }
    }
}
