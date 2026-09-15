using Blish_HUD.Controls;

namespace Quarry.Interfaces
{
    public interface IFormattedLabelHtmlService
    {
        FormattedLabelBuilder CreateLabel(string textWithHtml);
    }
}