// using System.Net;
// using System.Text;
// using CafeChain.Application.DTOs.AI;
// using CafeChain.Application.Services.AI;
// using CafeChain.Infrastructure.Configurations;
// using Microsoft.AspNetCore.Hosting;
// using Microsoft.Extensions.Logging.Abstractions;
// using Microsoft.Extensions.Options;
// using Moq;
// using SixLabors.ImageSharp;
// using SixLabors.ImageSharp.PixelFormats;

// namespace CafeChain.Tests;

// public sealed class AIImageHttpClientTests
// {
//     [Fact]
//     public async Task Pexels_search_retries_429_and_returns_all_candidates_without_random_selection()
//     {
//         var calls = 0;
//         var handler = new StubHandler(async request =>
//         {
//             calls++;
//             Assert.Equal("secret-from-options", request.Headers.Authorization?.ToString());
//             Assert.Contains("peach%20tea", request.RequestUri!.Query, StringComparison.OrdinalIgnoreCase);
//             if (calls == 1) return new HttpResponseMessage(HttpStatusCode.TooManyRequests);
//             return JsonResponse("""
//                 {"photos":[
//                   {"id":1,"width":1000,"height":1000,"url":"https://www.pexels.com/photo/1","photographer":"A","photographer_url":"https://www.pexels.com/a","alt":"peach tea","avg_color":"#cc8844","src":{"medium":"https://images.pexels.com/photos/1/m.jpeg","large":"https://images.pexels.com/photos/1/l.jpeg","large2x":"https://images.pexels.com/photos/1/xl.jpeg"}},
//                   {"id":2,"width":900,"height":900,"url":"https://www.pexels.com/photo/2","photographer":"B","photographer_url":"https://www.pexels.com/b","alt":"fruit tea","avg_color":"#aa7733","src":{"medium":"https://images.pexels.com/photos/2/m.jpeg","large":"https://images.pexels.com/photos/2/l.jpeg","large2x":"https://images.pexels.com/photos/2/xl.jpeg"}}
//                 ]}
//                 """);
//         });
//         var client = CreatePexelsClient(handler);

//         var result = await client.SearchAsync(new PexelsSearchRequestDTO
//         {
//             Query = "peach tea", Orientation = "square", PerPage = 15
//         });

//         Assert.True(result.Success, result.ErrorMessage);
//         Assert.Equal(2, result.Photos.Count);
//         Assert.Equal([1L, 2L], result.Photos.Select(x => x.Id));
//         Assert.Equal(2, calls);
//     }

//     [Fact]
//     public async Task Pexels_search_never_accepts_non_pexels_image_hosts()
//     {
//         var handler = new StubHandler(_ => Task.FromResult(JsonResponse("""
//             {"photos":[{"id":9,"width":1000,"height":1000,"url":"https://www.pexels.com/photo/9","alt":"tea","src":{"medium":"https://evil.example/9.jpg","large":"https://evil.example/9.jpg","large2x":"https://evil.example/9.jpg"}}]}
//             """)));

//         var result = await CreatePexelsClient(handler).SearchAsync(new PexelsSearchRequestDTO { Query = "tea" });

//         Assert.True(result.Success);
//         Assert.Empty(result.Photos);
//     }

//     [Fact]
//     public async Task Comfy_client_uploads_reference_configures_img2img_and_downloads_every_output()
//     {
//         string? promptBody = null;
//         var png = CreatePng();
//         var handler = new StubHandler(async request =>
//         {
//             var path = request.RequestUri!.AbsolutePath;
//             if (path == "/upload/image") return JsonResponse("{\"name\":\"uploaded-reference.png\"}");
//             if (path == "/prompt")
//             {
//                 promptBody = await request.Content!.ReadAsStringAsync();
//                 return JsonResponse("{\"prompt_id\":\"prompt-123\"}");
//             }
//             if (path.StartsWith("/history/", StringComparison.Ordinal))
//                 return JsonResponse("""
//                     {"prompt-123":{"outputs":{"9":{"images":[
//                       {"filename":"one.png","subfolder":"","type":"output"},
//                       {"filename":"two.png","subfolder":"","type":"output"},
//                       {"filename":"three.png","subfolder":"","type":"output"}
//                     ]}}}}
//                     """);
//             if (path == "/view")
//                 return new HttpResponseMessage(HttpStatusCode.OK)
//                 {
//                     Content = new ByteArrayContent(png) { Headers = { ContentType = new("image/png") } }
//                 };
//             return new HttpResponseMessage(HttpStatusCode.NotFound);
//         });
//         var contentRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "CafeChain"));
//         var environment = new Mock<IWebHostEnvironment>();
//         environment.SetupGet(x => x.ContentRootPath).Returns(contentRoot);
//         var options = Options.Create(new ComfyUIOptions
//         {
//             Enabled = true,
//             CheckpointName = "checkpoint.safetensors",
//             WorkflowPath = "Resources/AI/ComfyUI/product-img2img.json",
//             TimeoutSeconds = 10,
//             PollIntervalMilliseconds = 1,
//             MaxImageBytes = 2 * 1024 * 1024
//         });
//         var client = new ComfyUIClient(
//             new HttpClient(handler) { BaseAddress = new("http://localhost:8188") },
//             options, environment.Object, NullLogger<ComfyUIClient>.Instance);

//         var result = await client.GenerateImageAsync(new ComfyUIImageRequestDTO
//         {
//             ReferenceImageBytes = png,
//             ReferenceContentType = "image/png",
//             PositivePrompt = "peach tea product photography",
//             NegativePrompt = "people, text",
//             OutputCount = 3,
//             Denoise = 0.55
//         });

//         Assert.True(result.Success);
//         Assert.Equal(3, result.Images.Count);
//         Assert.Contains("uploaded-reference.png", promptBody);
//         Assert.Contains("\"amount\":3", promptBody);
//         Assert.Contains("\"denoise\":0.55", promptBody);
//     }

//     [Fact]
//     public async Task Comfy_text_mode_skips_upload_and_configures_batch_three_with_full_denoise()
//     {
//         string? promptBody = null;
//         var uploadCalls = 0;
//         var png = CreatePng();
//         var handler = new StubHandler(async request =>
//         {
//             var path = request.RequestUri!.AbsolutePath;
//             if (path == "/upload/image")
//             {
//                 uploadCalls++;
//                 return JsonResponse("{\"name\":\"unexpected.png\"}");
//             }
//             if (path == "/prompt")
//             {
//                 promptBody = await request.Content!.ReadAsStringAsync();
//                 return JsonResponse("{\"prompt_id\":\"text-123\"}");
//             }
//             if (path.StartsWith("/history/", StringComparison.Ordinal))
//                 return JsonResponse("""
//                     {"text-123":{"outputs":{"9":{"images":[
//                       {"filename":"one.png","subfolder":"","type":"output"},
//                       {"filename":"two.png","subfolder":"","type":"output"},
//                       {"filename":"three.png","subfolder":"","type":"output"}
//                     ]}}}}
//                     """);
//             if (path == "/view")
//                 return new HttpResponseMessage(HttpStatusCode.OK)
//                 { Content = new ByteArrayContent(png) { Headers = { ContentType = new("image/png") } } };
//             return new HttpResponseMessage(HttpStatusCode.NotFound);
//         });
//         var contentRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "CafeChain"));
//         var environment = new Mock<IWebHostEnvironment>();
//         environment.SetupGet(x => x.ContentRootPath).Returns(contentRoot);
//         var client = new ComfyUIClient(
//             new HttpClient(handler) { BaseAddress = new("http://localhost:8188") },
//             Options.Create(new ComfyUIOptions
//             {
//                 Enabled = true, CheckpointName = "checkpoint.safetensors",
//                 TextToImageWorkflowPath = "Resources/AI/ComfyUI/product-txt2img.json",
//                 TimeoutSeconds = 60, PollIntervalMilliseconds = 1, MaxImageBytes = 2 * 1024 * 1024
//             }), environment.Object, NullLogger<ComfyUIClient>.Instance);

//         var result = await client.GenerateImageAsync(new ComfyUIImageRequestDTO
//         {
//             GenerationMode = ComfyUIGenerationMode.TextToImage,
//             PositivePrompt = "detailed peach tea product photography", NegativePrompt = "text",
//             OutputCount = 3, Width = 1024, Height = 1024
//         });

//         Assert.True(result.Success, result.ErrorMessage);
//         Assert.Equal(3, result.Images.Count);
//         Assert.Equal(0, uploadCalls);
//         Assert.Contains("\"batch_size\":3", promptBody);
//         Assert.Contains("\"denoise\":1", promptBody);
//         Assert.DoesNotContain("uploaded-reference", promptBody);
//     }

//     private static PexelsClient CreatePexelsClient(HttpMessageHandler handler) => new(
//         new HttpClient(handler) { BaseAddress = new("https://api.pexels.com") },
//         Options.Create(new PexelsOptions { Enabled = true, ApiKey = "secret-from-options", TimeoutSeconds = 60 }),
//         Options.Create(new AIImagePipelineOptions { RetryCount = 1, RetryDelayMilliseconds = 1 }),
//         NullLogger<PexelsClient>.Instance);

//     private static HttpResponseMessage JsonResponse(string json) => new(HttpStatusCode.OK)
//     {
//         Content = new StringContent(json, Encoding.UTF8, "application/json")
//     };

//     private static byte[] CreatePng()
//     {
//         using var image = new Image<Rgba32>(512, 512);
//         using var stream = new MemoryStream();
//         image.SaveAsPng(stream);
//         return stream.ToArray();
//     }

//     private sealed class StubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler) : HttpMessageHandler
//     {
//         protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
//             handler(request);
//     }
// }
