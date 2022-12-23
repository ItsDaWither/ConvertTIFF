using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Blobs;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using ImageProcessor;
using ImageProcessor.Imaging.Formats;

namespace ConvertTIFF;

public static class ConvertTIFF
{
    [FunctionName("ConvertTIFF")]
    public static async Task RunAsync(
        [BlobTrigger("samples-workitems/{name}.tif{extension}", Connection = "")] Stream myBlob,
        [Blob("samples-workitems", FileAccess.Write)] BlobContainerClient outputImage, string name, string extension,
        ILogger log)
    {
        // Create one stream per TIFF Page
        List<MemoryStream> outputFile = Split(myBlob);
        int i = 0;
        foreach (MemoryStream stream in outputFile)
        {
            stream.Position = 0;
            // Upload PNGs to Blob Storage
            try
            {
                outputImage.UploadBlob($"{name}-p{i}.png", stream);
            }
            catch (RequestFailedException)
            {
                var blobClient = outputImage.GetBlobClient($"{name}-p{i}.png");
                await blobClient.UploadAsync(stream, true);
            }
            i++;
        }
    }

    private static List<MemoryStream> Split(Stream inputFile)
    {
        // Get the frame dimension list from the image of the file and 
        Image tiffImage = Image.FromStream(inputFile);

        // Gets the total number of pages in the .tiff file 
        int noOfPages = tiffImage.GetFrameCount(FrameDimension.Page);

        // Check for existence of TIFF Decoder
        ImageCodecInfo[] imageEncoders = ImageCodecInfo.GetImageEncoders();
        ImageCodecInfo encodeInfo = imageEncoders.FirstOrDefault(t => t.MimeType == "image/tiff");
        var outputFile = new List<MemoryStream>(new MemoryStream[noOfPages]);

        for (int index = 0; index < noOfPages; index++)
        {
            tiffImage.SelectActiveFrame(FrameDimension.Page, index);
            // Save as PNG
            try
            {
                tiffImage.Save(outputFile[index], ImageFormat.Png);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        return outputFile;
    }
}