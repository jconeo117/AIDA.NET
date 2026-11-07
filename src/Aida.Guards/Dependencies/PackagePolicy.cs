using System.Collections.Frozen;

namespace Aida.Guards.Dependencies;

/// <summary>Las familias de paquetes con sitio restringido (reglas A1 y D4).</summary>
/// <remarks>
/// A1: el núcleo decide sin modelo de lenguaje (ADR 0001). D4: lo nativo y pesado, solo en
/// <c>Aida.Capture</c> (ADR 0017). <c>Aida.Web</c> y <c>Aida.Cli</c> están permitidos porque, al componerlo
/// todo, sus locks heredan los paquetes de los adaptadores.
/// </remarks>
public static class PackagePolicy
{
    /// <summary>Las reglas, en orden fijo: A1 y luego D4.</summary>
    /// <remarks>Inmutables, por C4.</remarks>
    public static IReadOnlyList<PackageRule> Rules { get; } =
    [
        new PackageRule(
            "A1",
            [
                "OpenAI", "Azure.AI.OpenAI", "Azure.AI.Inference", "Anthropic", "Microsoft.Extensions.AI",
                "Microsoft.SemanticKernel", "LLamaSharp", "OllamaSharp", "Mistral", "Cohere",
                "ModelContextProtocol", "Microsoft.ML.Tokenizers", "LangChain",
            ],
            new[] { "Aida.Cognition", "Aida.Web", "Aida.Cli" }.ToFrozenSet(StringComparer.Ordinal)),
        new PackageRule(
            "D4",
            [
                "Whisper.net", "NAudio", "Vosk", "Tesseract", "UglyToad.PdfPig", "PdfPig", "PDFtoImage",
                "PdfiumViewer", "Docnet.Core", "SkiaSharp", "Microsoft.ML.OnnxRuntime", "OpenCvSharp",
                "Emgu.CV", "FFMpegCore", "org.k2fsa.sherpa.onnx", "PortAudioSharp",
            ],
            new[] { "Aida.Capture", "Aida.Web", "Aida.Cli" }.ToFrozenSet(StringComparer.Ordinal)),
    ];
}
