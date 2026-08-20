using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;
using System.Text;

[ApiController]
public class QzController : ControllerBase
{
    private readonly IWebHostEnvironment _env;

    public QzController(IWebHostEnvironment env)
    {
        _env = env;
    }

    [HttpGet("/qz/digital-certificate.txt")]
    public IActionResult Certificate()
    {
        var path = Path.Combine(
            _env.ContentRootPath,
            "qz",
            "digital-certificate.txt"
        );

        if (!System.IO.File.Exists(path))
            return NotFound("QZ certificate was not found.");

        return PhysicalFile(path, "text/plain");
    }

    [HttpPost("/qz/sign")]
    [Consumes("text/plain")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Sign()
    {
        using var reader = new StreamReader(
            Request.Body,
            Encoding.UTF8
        );

        var request = await reader.ReadToEndAsync();

        if (string.IsNullOrWhiteSpace(request))
            return BadRequest("No QZ signing request was supplied.");

        var keyPath = Path.Combine(
            _env.ContentRootPath,
            "qz",
            "private-key.pem"
        );

        if (!System.IO.File.Exists(keyPath))
            return NotFound("QZ private key was not found.");

        var pem = await System.IO.File.ReadAllTextAsync(keyPath);

        using var rsa = RSA.Create();
        rsa.ImportFromPem(pem);

        var signature = rsa.SignData(
            Encoding.UTF8.GetBytes(request),
            HashAlgorithmName.SHA512,
            RSASignaturePadding.Pkcs1
        );

        return Content(
            Convert.ToBase64String(signature),
            "text/plain",
            Encoding.UTF8
        );
    }
}