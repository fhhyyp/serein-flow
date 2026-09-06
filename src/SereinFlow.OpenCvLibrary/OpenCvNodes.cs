using System;
using OpenCvSharp;
using SereinFlow.Core.Api;
using SereinFlow.Library;
using SereinFlow.Runtime.Abstractions;

namespace SereinFlow.OpenCvLibrary;

/// <summary>
/// Converts an in-process OpenCV matrix into a small JSON-safe result summary.
/// 将进程内 OpenCV 矩阵转换为小型 JSON 安全结果摘要。
/// </summary>
public sealed class MatConverter : INodeResultConverter<Mat, object>
{
    /// <summary>
    /// Returns matrix dimensions and pixel format without serializing the matrix buffer.
    /// 返回矩阵尺寸和像素格式，不序列化矩阵缓冲区。
    /// </summary>
    public object Transfer(Mat primitive)
    {
        ArgumentNullException.ThrowIfNull(primitive);
        return new
        {
            type = "opencv-mat",
            width = primitive.Width,
            height = primitive.Height,
            channels = primitive.Channels(),
            depth = $"{primitive.Depth()}",
            empty = primitive.Empty(),
        };
    }
}

/// <summary>
/// Common OpenCV image-processing nodes. Every method is an ordinary Action node and uploads one
/// PNG workpiece for the result before returning the original Mat to downstream nodes.
/// 常用 OpenCV 图像处理节点。每个方法都是普通 Action 节点，在返回 Mat 给下游节点前上传一次 PNG 工件。
/// </summary>
[FlowLibrary("SereinFlow OpenCV 图像处理")]
public sealed class OpenCvNodes
{
    private readonly IFlowWorkpiece _flowWorkpiece;

    /// <summary>
    /// Creates the node container with the run-scoped workpiece service.
    /// 使用运行级工件服务创建节点容器。
    /// </summary>
    public OpenCvNodes(IFlowWorkpiece flowWorkpiece)
        => _flowWorkpiece = flowWorkpiece ?? throw new ArgumentNullException(nameof(flowWorkpiece));

    /// <summary>
    /// Creates a deterministic BGR sample image for demos and smoke tests.
    /// 创建用于演示和冒烟测试的确定性 BGR 示例图像。
    /// </summary>
    [FlowNode(AnotherName = "生成示例图像", Desc = "生成可直接用于 OpenCV 处理节点的彩色示例图像。")]
    [NodeResult<MatConverter>]
    public Mat 生成示例图像(
        IFlowContext flowContext,
        [NodeParam(Name = "图像宽度", IsExplicit = false)] int width = 640,
        [NodeParam(Name = "图像高度", IsExplicit = false)] int height = 480)
    {
        ValidateDimensions(width, height);

        var image = new Mat(new Size(width, height), MatType.CV_8UC3, new Scalar(18, 24, 32));
        var center = new Point(width / 2, height / 2);
        var radius = Math.Max(8, Math.Min(width, height) / 5);

        Cv2.Rectangle(image, new Rect(width / 10, height / 8, width / 3, height / 3), new Scalar(40, 160, 220), -1);
        Cv2.Circle(image, center, radius, new Scalar(220, 120, 45), -1);
        Cv2.Line(image, new Point(width / 8, height * 3 / 4), new Point(width * 7 / 8, height / 4), new Scalar(80, 220, 90), 8);
        Cv2.PutText(image, "OpenCV", new Point(width / 5, height * 9 / 10), HersheyFonts.HersheySimplex, 1.2, Scalar.White, 2);

        return Publish(image, "opencv-sample.png", flowContext);
    }

    /// <summary>
    /// Decodes encoded image bytes into a Mat.
    /// 将编码图像字节解码为 Mat。
    /// </summary>
    [FlowNode(AnotherName = "读取图像", Desc = "将 PNG、JPEG 等编码图像字节读取为 OpenCV Mat。")]
    [NodeResult<MatConverter>]
    public Mat 读取图像([NodeParam(Name = "图像字节")] byte[] imageBytes, IFlowContext flowContext)
    {
        ArgumentNullException.ThrowIfNull(imageBytes);
        if (imageBytes.Length == 0)
            throw new ArgumentException("图像字节不能为空。", nameof(imageBytes));

        var image = Cv2.ImDecode(imageBytes, ImreadModes.Unchanged);
        if (image.Empty())
        {
            image.Dispose();
            throw new ArgumentException("图像字节不是受支持的 PNG、JPEG 或其他编码图像。", nameof(imageBytes));
        }

        return Publish(image, "opencv-decoded.png", flowContext);
    }

    /// <summary>
    /// Converts a BGR/BGRA image to one-channel grayscale.
    /// 将 BGR/BGRA 图像转换为单通道灰度图。
    /// </summary>
    [FlowNode(AnotherName = "灰度化", Desc = "将彩色图像转换为单通道灰度图。")]
    [NodeResult<MatConverter>]
    public Mat 灰度化([NodeParam(Name = "输入图像")] Mat image, IFlowContext flowContext)
    {
        EnsureImage(image);
        if (image.Channels() == 1)
            return Publish(image.Clone(), "opencv-grayscale.png", flowContext);

        var result = new Mat();
        try
        {
            Cv2.CvtColor(image, result, image.Channels() == 4 ? ColorConversionCodes.BGRA2GRAY : ColorConversionCodes.BGR2GRAY);
            return Publish(result, "opencv-grayscale.png", flowContext);
        }
        catch
        {
            result.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Applies a binary threshold and returns a new binary Mat.
    /// 应用二值阈值并返回新的二值 Mat。
    /// </summary>
    [FlowNode(AnotherName = "二值化", Desc = "按阈值将图像转换为黑白二值图，彩色输入会先灰度化。")]
    [NodeResult<MatConverter>]
    public Mat 二值化(
        [NodeParam(Name = "输入图像")] Mat image,
        IFlowContext flowContext,
        [NodeParam(Name = "阈值", IsExplicit = false)] double threshold = 127,
        [NodeParam(Name = "最大值", IsExplicit = false)] double maxValue = 255)
    {
        EnsureImage(image);
        ValidateRange(threshold, 0, 255, nameof(threshold));
        ValidateRange(maxValue, 1, 255, nameof(maxValue));

        using var grayscale = ToGrayscale(image);
        var result = new Mat();
        try
        {
            Cv2.Threshold(grayscale, result, threshold, maxValue, ThresholdTypes.Binary);
            return Publish(result, "opencv-threshold.png", flowContext);
        }
        catch
        {
            result.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Dilates foreground regions using a rectangular kernel.
    /// 使用矩形核膨胀前景区域。
    /// </summary>
    [FlowNode(AnotherName = "膨胀", Desc = "使用矩形结构元素扩大二值图中的前景区域。")]
    [NodeResult<MatConverter>]
    public Mat 膨胀(
        [NodeParam(Name = "输入图像")] Mat image,
        IFlowContext flowContext,
        [NodeParam(Name = "核尺寸", IsExplicit = false)] int kernelSize = 3,
        [NodeParam(Name = "迭代次数", IsExplicit = false)] int iterations = 1)
        => Morphology(image, kernelSize, iterations, MorphTypes.Dilate, "opencv-dilate.png", flowContext);

    /// <summary>
    /// Erodes foreground regions using a rectangular kernel.
    /// 使用矩形结构元素腐蚀前景区域。
    /// </summary>
    [FlowNode(AnotherName = "腐蚀", Desc = "使用矩形结构元素收缩二值图中的前景区域。")]
    [NodeResult<MatConverter>]
    public Mat 腐蚀(
        [NodeParam(Name = "输入图像")] Mat image,
        IFlowContext flowContext,
        [NodeParam(Name = "核尺寸", IsExplicit = false)] int kernelSize = 3,
        [NodeParam(Name = "迭代次数", IsExplicit = false)] int iterations = 1)
        => Morphology(image, kernelSize, iterations, MorphTypes.Erode, "opencv-erode.png", flowContext);

    /// <summary>
    /// Removes small foreground noise using morphological opening.
    /// 使用形态学开运算去除小块前景噪声。
    /// </summary>
    [FlowNode(AnotherName = "开运算", Desc = "先腐蚀后膨胀，用于去除小块前景噪声。")]
    [NodeResult<MatConverter>]
    public Mat 开运算(
        [NodeParam(Name = "输入图像")] Mat image,
        IFlowContext flowContext,
        [NodeParam(Name = "核尺寸", IsExplicit = false)] int kernelSize = 3,
        [NodeParam(Name = "迭代次数", IsExplicit = false)] int iterations = 1)
        => Morphology(image, kernelSize, iterations, MorphTypes.Open, "opencv-open.png", flowContext);

    /// <summary>
    /// Fills small holes using morphological closing.
    /// 使用形态学闭运算填充小孔洞。
    /// </summary>
    [FlowNode(AnotherName = "闭运算", Desc = "先膨胀后腐蚀，用于填充小孔洞并连接邻近区域。")]
    [NodeResult<MatConverter>]
    public Mat 闭运算(
        [NodeParam(Name = "输入图像")] Mat image,
        IFlowContext flowContext,
        [NodeParam(Name = "核尺寸", IsExplicit = false)] int kernelSize = 3,
        [NodeParam(Name = "迭代次数", IsExplicit = false)] int iterations = 1)
        => Morphology(image, kernelSize, iterations, MorphTypes.Close, "opencv-close.png", flowContext);

    /// <summary>
    /// Applies Gaussian blur with an odd square kernel.
    /// 使用奇数方形核应用高斯模糊。
    /// </summary>
    [FlowNode(AnotherName = "高斯模糊", Desc = "使用高斯滤波平滑图像并降低噪声。")]
    [NodeResult<MatConverter>]
    public Mat 高斯模糊(
        [NodeParam(Name = "输入图像")] Mat image,
        IFlowContext flowContext,
        [NodeParam(Name = "核尺寸", IsExplicit = false)] int kernelSize = 5,
        [NodeParam(Name = "X 方向标准差", IsExplicit = false)] double sigmaX = 0)
    {
        EnsureImage(image);
        ValidateKernel(kernelSize);
        if (sigmaX < 0)
            throw new ArgumentOutOfRangeException(nameof(sigmaX), "X 方向标准差不能为负数。");

        var result = new Mat();
        try
        {
            Cv2.GaussianBlur(image, result, new Size(kernelSize, kernelSize), sigmaX);
            return Publish(result, "opencv-gaussian-blur.png", flowContext);
        }
        catch
        {
            result.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Detects edges with the Canny algorithm.
    /// 使用 Canny 算法检测边缘。
    /// </summary>
    [FlowNode(AnotherName = "Canny 边缘检测", Desc = "使用 Canny 算法生成二值边缘图。")]
    [NodeResult<MatConverter>]
    public Mat Canny边缘检测(
        [NodeParam(Name = "输入图像")] Mat image,
        IFlowContext flowContext,
        [NodeParam(Name = "低阈值", IsExplicit = false)] double lowThreshold = 50,
        [NodeParam(Name = "高阈值", IsExplicit = false)] double highThreshold = 150)
    {
        EnsureImage(image);
        if (lowThreshold < 0 || highThreshold <= lowThreshold)
            throw new ArgumentOutOfRangeException(nameof(lowThreshold), "高阈值必须大于非负的低阈值。");

        using var grayscale = ToGrayscale(image);
        var result = new Mat();
        try
        {
            Cv2.Canny(grayscale, result, lowThreshold, highThreshold);
            return Publish(result, "opencv-canny.png", flowContext);
        }
        catch
        {
            result.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Resizes an image to the requested dimensions.
    /// 将图像缩放到指定尺寸。
    /// </summary>
    [FlowNode(AnotherName = "缩放图像", Desc = "将图像缩放到指定像素宽高。")]
    [NodeResult<MatConverter>]
    public Mat 缩放图像(
        [NodeParam(Name = "输入图像")] Mat image,
        IFlowContext flowContext,
        [NodeParam(Name = "宽度")] int width,
        [NodeParam(Name = "高度")] int height)
    {
        EnsureImage(image);
        ValidateDimensions(width, height);

        var result = new Mat();
        try
        {
            Cv2.Resize(image, result, new Size(width, height), 0, 0, InterpolationFlags.Area);
            return Publish(result, "opencv-resized.png", flowContext);
        }
        catch
        {
            result.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Inverts all pixels in an image.
    /// 反转图像中的全部像素。
    /// </summary>
    [FlowNode(AnotherName = "反色", Desc = "对图像执行逐像素反色处理。")]
    [NodeResult<MatConverter>]
    public Mat 反色([NodeParam(Name = "输入图像")] Mat image, IFlowContext flowContext)
    {
        EnsureImage(image);

        var result = new Mat();
        try
        {
            Cv2.BitwiseNot(image, result);
            return Publish(result, "opencv-inverted.png", flowContext);
        }
        catch
        {
            result.Dispose();
            throw;
        }
    }

    private Mat Morphology(Mat image, int kernelSize, int iterations, MorphTypes operation, string fileName, IFlowContext flowContext)
    {
        EnsureImage(image);
        ValidateKernel(kernelSize);
        if (iterations is < 1 or > 20)
            throw new ArgumentOutOfRangeException(nameof(iterations), "迭代次数必须在 1 到 20 之间。");

        using var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(kernelSize, kernelSize));
        var result = new Mat();
        try
        {
            Cv2.MorphologyEx(image, result, operation, kernel, iterations: iterations);
            return Publish(result, fileName, flowContext);
        }
        catch
        {
            result.Dispose();
            throw;
        }
    }

    private Mat Publish(Mat result, string fileName, IFlowContext flowContext)
    {
        if (result.Empty())
        {
            result.Dispose();
            throw new InvalidOperationException("OpenCV 处理产生了空图像。");
        }

        try
        {
            Cv2.ImEncode(".png", result, out var encoded);
            _ = _flowWorkpiece.UploadNodeOutput(flowContext, fileName, encoded, FlowWorkpieceContentTypes.Png);
            return result;
        }
        catch
        {
            result.Dispose();
            throw;
        }
    }

    private static Mat ToGrayscale(Mat image)
    {
        if (image.Channels() == 1)
            return image.Clone();

        var grayscale = new Mat();
        try
        {
            Cv2.CvtColor(image, grayscale, image.Channels() == 4 ? ColorConversionCodes.BGRA2GRAY : ColorConversionCodes.BGR2GRAY);
            return grayscale;
        }
        catch
        {
            grayscale.Dispose();
            throw;
        }
    }

    private static void EnsureImage(Mat image)
    {
        ArgumentNullException.ThrowIfNull(image);
        if (image.Empty())
            throw new ArgumentException("输入图像不能为空。", nameof(image));
    }

    private static void ValidateKernel(int kernelSize)
    {
        if (kernelSize is < 1 or > 99 || kernelSize % 2 == 0)
            throw new ArgumentOutOfRangeException(nameof(kernelSize), "核尺寸必须是 1 到 99 之间的奇数。");
    }

    private static void ValidateDimensions(int width, int height)
    {
        if (width is < 16 or > 16_384 || height is < 16 or > 16_384)
            throw new ArgumentOutOfRangeException(nameof(width), "图像宽高必须在 16 到 16384 像素之间。");
    }

    private static void ValidateRange(double value, double minimum, double maximum, string parameterName)
    {
        if (double.IsNaN(value) || value < minimum || value > maximum)
            throw new ArgumentOutOfRangeException(parameterName, $"参数必须在 {minimum} 到 {maximum} 之间。");
    }
}
