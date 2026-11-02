namespace EthangRss.Core.Content;

/// <summary>A piece of inline formatting inside a paragraph.</summary>
public sealed record InlineRun(string Text, bool IsBold = false, bool IsItalic = false, bool IsCode = false, string? Href = null);

/// <summary>One rendered unit of an article body.</summary>
public abstract record ContentBlock;

public sealed record HeadingBlock(int Level, string Text) : ContentBlock;

public sealed record ParagraphBlock(IReadOnlyList<InlineRun> Runs) : ContentBlock
{
    public string PlainText => string.Concat(Runs.Select(run => run.Text));
}

public sealed record ListBlock(bool IsOrdered, IReadOnlyList<string> Items) : ContentBlock;

public sealed record QuoteBlock(IReadOnlyList<ContentBlock> Children) : ContentBlock;

public sealed record CodeBlock(string Text) : ContentBlock;

public sealed record ImageBlock(string Url, string Alt) : ContentBlock;

public sealed record SeparatorBlock : ContentBlock;

/// <summary>A parsed article body, ready for a platform to render natively.</summary>
public sealed record ArticleBody(IReadOnlyList<ContentBlock> Blocks)
{
    public static ArticleBody Empty => new(Array.Empty<ContentBlock>());

    public bool IsEmpty => Blocks.Count == 0;
}
