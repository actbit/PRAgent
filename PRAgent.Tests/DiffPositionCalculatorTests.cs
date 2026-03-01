using Xunit;

namespace PRAgent.Tests;

/// <summary>
/// DiffPositionCalculatorのテスト
/// </summary>
public class DiffPositionCalculatorTests
{
    /// <summary>
    /// テスト用のdiff計算メソッド（GitHubServiceと同じロジック）
    /// </summary>
    private static int? CalculateDiffPosition(string? patch, int lineNumber)
    {
        if (string.IsNullOrEmpty(patch))
            return null;

        var lines = patch.Split('\n');
        int position = 0;
        int currentNewLine = 0;

        foreach (var line in lines)
        {
            position++;

            // Hunk headerを解析
            var hunkMatch = System.Text.RegularExpressions.Regex.Match(line, @"^@@\s+-\d+(?:,\d+)?\s+\+(\d+)(?:,\d+)?\s+@@");
            if (hunkMatch.Success)
            {
                // 開始行番号の1つ前に設定（次の行でインクリメントして正しい行番号になるように）
                currentNewLine = int.Parse(hunkMatch.Groups[1].Value) - 1;
                continue;
            }

            // 行のタイプを判定
            if (line.StartsWith("+"))
            {
                currentNewLine++;
                if (currentNewLine == lineNumber)
                {
                    return position;
                }
            }
            else if (line.StartsWith("-"))
            {
                // 削除行はスキップ
            }
            else if (line.StartsWith(" ") || line == "")
            {
                currentNewLine++;
                if (currentNewLine == lineNumber)
                {
                    return position;
                }
            }
        }

        return null;
    }

    [Fact]
    public void CalculateDiffPosition_SimpleAddition_ReturnsCorrectPosition()
    {
        // Arrange
        // diffは以下のようになる（行番号はdiff内のposition）:
        // 1: @@ -1,3 +1,4 @@    <- hunk header (position=1)
        // 2:  line1             <- position=2, ファイル内1行目
        // 3:  line2             <- position=3, ファイル内2行目
        // 4: +newLine           <- position=4, ファイル内3行目（追加）
        // 5:  line3             <- position=5, ファイル内4行目
        var patch = "@@ -1,3 +1,4 @@\n line1\n line2\n+newLine\n line3";

        // Act & Assert
        Assert.Equal(2, CalculateDiffPosition(patch, 1)); // line1
        Assert.Equal(3, CalculateDiffPosition(patch, 2)); // line2
        Assert.Equal(4, CalculateDiffPosition(patch, 3)); // newLine (added)
        Assert.Equal(5, CalculateDiffPosition(patch, 4)); // line3
    }

    [Fact]
    public void CalculateDiffPosition_AddedLine_ReturnsCorrectPosition()
    {
        // Arrange
        var patch = "@@ -1,3 +1,4 @@\n line1\n line2\n+newLine\n line3";

        // Act - newLineはファイル内の3行目（追加された行）
        var result = CalculateDiffPosition(patch, 3);

        // Assert - position 4は "+newLine" の行
        Assert.Equal(4, result);
    }

    [Fact]
    public void CalculateDiffPosition_AfterAddedLine_ReturnsCorrectPosition()
    {
        // Arrange
        var patch = "@@ -1,3 +1,4 @@\n line1\n line2\n+newLine\n line3";

        // Act - line3は追加後のファイル内の4行目
        var result = CalculateDiffPosition(patch, 4);

        // Assert - position 5は " line3" の行
        // diff: 1=hunk, 2=line1, 3=line2, 4=+newLine, 5=line3
        Assert.Equal(5, result);
    }

    [Fact]
    public void CalculateDiffPosition_WithDeletion_SkipsDeletedLine()
    {
        // Arrange
        // diff:
        // 1: @@ -1,4 +1,3 @@
        // 2:  line1          <- ファイル内1行目
        // 3: -oldLine2       <- 削除行（ファイル内行番号は進まない）
        // 4:  line3          <- ファイル内2行目
        // 5:  line4          <- ファイル内3行目
        var patch = "@@ -1,4 +1,3 @@\n line1\n-oldLine2\n line3\n line4";

        // Act & Assert
        Assert.Equal(2, CalculateDiffPosition(patch, 1)); // line1
        Assert.Equal(4, CalculateDiffPosition(patch, 2)); // line3 (削除行の後)
        Assert.Equal(5, CalculateDiffPosition(patch, 3)); // line4
    }

    [Fact]
    public void CalculateDiffPosition_LineNotFound_ReturnsNull()
    {
        // Arrange
        var patch = "@@ -1,2 +1,2 @@\n line1\n line2";

        // Act - 行番号100は存在しない
        var result = CalculateDiffPosition(patch, 100);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void CalculateDiffPosition_EmptyPatch_ReturnsNull()
    {
        // Arrange
        var patch = "";

        // Act
        var result = CalculateDiffPosition(patch, 1);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void CalculateDiffPosition_NullPatch_ReturnsNull()
    {
        // Arrange
        string? patch = null;

        // Act
        var result = CalculateDiffPosition(patch, 1);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void CalculateDiffPosition_MultipleHunks_ReturnsCorrectPosition()
    {
        // Arrange - 複数のhunkがあるケース
        // 1: @@ -1,3 +1,4 @@   <- hunk 1 header
        // 2:  line1
        // 3:  line2
        // 4: +newLine
        // 5:  line3
        // 6: @@ -10,3 +11,4 @@ <- hunk 2 header (ファイル内11行目から開始)
        // 7:  line10           <- ファイル内11行目
        // 8:  line11           <- ファイル内12行目
        // 9: +anotherNewLine   <- ファイル内13行目
        // 10: line12           <- ファイル内14行目
        var patch = "@@ -1,3 +1,4 @@\n line1\n line2\n+newLine\n line3\n@@ -10,3 +11,4 @@\n line10\n line11\n+anotherNewLine\n line12";

        // Act & Assert
        Assert.Equal(9, CalculateDiffPosition(patch, 13)); // anotherNewLine
        Assert.Equal(10, CalculateDiffPosition(patch, 14)); // line12
    }

    [Fact]
    public void CalculateDiffPosition_HunkStartLine_ReturnsCorrectPosition()
    {
        // Arrange - hunkが+15から始まる場合
        // ファイル内の15行目からdiffが始まる
        var patch = "@@ -10,3 +15,4 @@\n line15\n line16\n+newLine\n line17";

        // Act & Assert
        Assert.Equal(2, CalculateDiffPosition(patch, 15)); // line15
        Assert.Equal(3, CalculateDiffPosition(patch, 16)); // line16
        Assert.Equal(4, CalculateDiffPosition(patch, 17)); // newLine
        Assert.Equal(5, CalculateDiffPosition(patch, 18)); // line17
    }

    [Fact]
    public void CalculateDiffPosition_RealWorldExample_WorksCorrectly()
    {
        // Arrange - 実際のGitHubのdiff例
        // hunkは+15から始まるので、ファイル内の15行目がdiffの最初の行
        var patch = "@@ -15,8 +15,10 @@ public class Example\n    public void Method1()\n    {\n        var x = 1;\n+        var y = 2;\n+        var z = 3;\n        Console.WriteLine(x);\n-        Console.WriteLine(\"old\");\n+        Console.WriteLine(\"new\");\n    }\n}";

        // diff position:
        // 1: @@ -15,8 +15,10 @@ public class Example
        // 2:     public void Method1()     <- ファイル内15行目
        // 3:     {                          <- ファイル内16行目
        // 4:         var x = 1;             <- ファイル内17行目
        // 5: +        var y = 2;            <- ファイル内18行目（追加）
        // 6: +        var z = 3;            <- ファイル内19行目（追加）
        // 7:         Console.WriteLine(x);  <- ファイル内20行目
        // 8: -        Console.WriteLine("old"); <- 削除行
        // 9: +        Console.WriteLine("new"); <- ファイル内21行目（追加）
        // 10:    }
        // 11: }

        // Act & Assert
        Assert.Equal(5, CalculateDiffPosition(patch, 18)); // var y = 2
        Assert.Equal(6, CalculateDiffPosition(patch, 19)); // var z = 3
        Assert.Equal(7, CalculateDiffPosition(patch, 20)); // Console.WriteLine(x)
        Assert.Equal(9, CalculateDiffPosition(patch, 21)); // Console.WriteLine("new")
    }
}
