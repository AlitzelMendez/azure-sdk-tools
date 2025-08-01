using Report.Models;

namespace Report.Helper;

public class IdentifyHandwrittenLines
{
    public static LinesAnalysis GetLinesAnalysis(ApiViewDocument apiViewDocument)
    {
        int totalLines = 0;
        int handwrittenLines = 0;
        foreach (var reviewLines in apiViewDocument.ReviewLines)
        {
            //I need to recursively analyze the lines and their children
            if (IsIgnoredLine(reviewLines))
            {
                continue;
            }

            Console.WriteLine(reviewLines.LineId);
            totalLines++;

            if (IsHandwrittenLine(reviewLines.Tokens))
            {
                handwrittenLines++;
            }

            if (reviewLines.Children.Count > 0)
            {
                if (apiViewDocument.Language == "Python" && !AreChildrenIndependentLines(reviewLines.Tokens))
                {
                    // If the language is Python and the children are not independent lines, we ignore them
                    continue;
                }

                var childAnalysis = AnalyzeChildren(apiViewDocument.Language, reviewLines.Children);
                

                totalLines += childAnalysis.TotalLines;
                handwrittenLines += childAnalysis.HandwrittenLines;

            }
        }

        return new LinesAnalysis() { TotalLines = totalLines, HandwrittenLines = handwrittenLines };
    }


    //Analyze all the children recursively returning total count and handwritten lines count
    private static LinesAnalysis AnalyzeChildren(string language, List<ReviewLine> children)
    {
        int totalLines = 0;
        int handwrittenLines = 0;
        foreach (var child in children)
        {
            if (IsIgnoredLine(child))
            {
                continue;
            }

            Console.WriteLine(child.LineId);
            totalLines++;
            if (IsHandwrittenLine(child.Tokens))
            {
                handwrittenLines++;
            }
            if (child.Children.Count > 0)
            {
                if (language != "Python" || AreChildrenIndependentLines(child.Tokens))
                {
                    var childAnalysis = AnalyzeChildren(language, child.Children);
                    totalLines += childAnalysis.TotalLines;
                    handwrittenLines += childAnalysis.HandwrittenLines;
                }
              
            }
        }
        return new LinesAnalysis() { TotalLines = totalLines, HandwrittenLines = handwrittenLines };
    }


    private static bool AreChildrenIndependentLines(List<Token> token)
    {
        var renderClasses = token.SelectMany(t => t.RenderClasses).ToList();
        return !renderClasses.Contains("method");

    }


    // Rules to ignore lines from total count
    // 1. Lines with no tokens (empty lines).
    // 2. Lines with decorators @
    // 3. Child of methods from Python
    private static bool IsIgnoredLine(ReviewLine line)
    {
        // Ignore empty lines
        if (string.IsNullOrWhiteSpace(line.LineId) && line.Tokens.Count == 0 && line.Children.Count == 0)
        {
            return true;
        }
        // Ignore lines with decorators (starting with @)
        if (line.Tokens.Count > 0 && line.Tokens.Any(t => t.Value.StartsWith("@")))
        {
            return true;
        }
       
        return false;
    }

    //Rule to identify handwritten lines:
    // RenderClasses list contains "handwritten"
    private static bool IsHandwrittenLine(List<Token> token)
    {
        if (token.Count == 0)
        {
            return false;
        }

        var renderClasses = token.SelectMany(t => t.RenderClasses).ToList();
        return renderClasses.Contains("handwritten");
    }
}
