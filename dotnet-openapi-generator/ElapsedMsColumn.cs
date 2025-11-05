using Spectre.Console;
using Spectre.Console.Rendering;

namespace dotnet.openapi.generator;

/// <summary>
/// A column showing the elapsed time of a task.
/// </summary>
internal class ElapsedMsColumn : ProgressColumn
{
    /// <inheritdoc/>
    protected override bool NoWrap => true;

    /// <summary>
    /// Gets or sets the style of the remaining time text.
    /// </summary>
    public Style Style { get; set; } = Color.Blue;

    /// <inheritdoc/>
    public override IRenderable Render(RenderOptions options, ProgressTask task, TimeSpan deltaTime)
    {
        var elapsed = task.ElapsedTime;

        if (elapsed is null)
        {
            return new Markup("-- ms");
        }

        return new Text($"{elapsed.Value.TotalMilliseconds:N2} ms", Style ?? Style.Plain);
    }
}