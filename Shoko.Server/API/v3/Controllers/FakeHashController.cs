using System;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.v3.Models.Shoko;
using Shoko.Server.Models;
using Shoko.Server.Repositories;
using Shoko.Server.Server;
using Shoko.Server.Settings;

namespace Shoko.Server.API.v3.Controllers;

[ApiController, Route("/api/v{version:apiVersion}/[controller]"), ApiV3]
[Authorize("admin")]
public class FakeHashController : BaseController
{
    public FakeHashController(ISettingsProvider settingsProvider) : base(settingsProvider) { }

    /// <summary>
    /// Store fake hashes and metadata for a file to prevent hashing.
    /// </summary>
    /// <param name="body">The fake hash data to store.</param>
    /// <returns>The file and location identifiers.</returns>
    [HttpPost]
    public ActionResult<FakeHash.Result> AddFakeHashes([FromBody] FakeHash.Body body)
    {
        if (body == null)
            return ValidationProblem("Missing Body.");

        if (!body.FileID.HasValue && (!body.ImportFolderID.HasValue || string.IsNullOrWhiteSpace(body.FilePath)))
            return ValidationProblem("Provide either fileID or importFolderID + filePath.");

        if (body.Hashes == null)
            return ValidationProblem("Missing hashes.", nameof(body.Hashes));

        if (!ModelState.IsValid)
            return base.ValidationProblem(ModelState);

        var now = DateTime.Now;
        SVR_VideoLocal vlocal = null;
        SVR_VideoLocal_Place place = null;
        var importFolder = default(SVR_ImportFolder);
        var relativePath = string.Empty;

        if (body.FileID.HasValue)
        {
            vlocal = RepoFactory.VideoLocal.GetByID(body.FileID.Value);
            if (vlocal == null)
                return NotFound($"No File entry for the given fileID={body.FileID.Value}.");

            place = vlocal.FirstValidPlace ?? vlocal.FirstResolvedPlace ?? vlocal.Places.FirstOrDefault();
        }

        if (place == null)
        {
            if (!body.ImportFolderID.HasValue || string.IsNullOrWhiteSpace(body.FilePath))
                return ValidationProblem("File location not found. Provide importFolderID + filePath to create it.");

            importFolder = RepoFactory.ImportFolder.GetByID(body.ImportFolderID.Value);
            if (importFolder == null)
                return NotFound($"No ImportFolder entry for the given importFolderID={body.ImportFolderID.Value}.");

            if (!TryGetRelativePath(importFolder, body.FilePath, out relativePath, out var pathError))
                return ValidationProblem(pathError, nameof(body.FilePath));

            place = RepoFactory.VideoLocalPlace.GetByFilePathAndImportFolderID(relativePath, importFolder.ImportFolderID);
            if (place?.VideoLocal is { } existingVideoLocal)
            {
                if (body.FileID.HasValue && existingVideoLocal.VideoLocalID != body.FileID.Value)
                    return ValidationProblem("The provided fileID does not match the existing file location.");

                vlocal = existingVideoLocal;
            }
        }

        if (vlocal == null)
        {
            vlocal = new SVR_VideoLocal
            {
                DateTimeCreated = body.DateCreated ?? now,
                DateTimeUpdated = body.DateUpdated ?? now,
                DateTimeImported = body.DateImported,
                FileName = Path.GetFileName(relativePath),
                Hash = string.Empty,
                CRC32 = string.Empty,
                MD5 = string.Empty,
                SHA1 = string.Empty,
                IsIgnored = false,
                IsVariation = false
            };
        }

        vlocal.Hash = NormalizeHash(body.Hashes.ED2K);
        vlocal.CRC32 = NormalizeHash(body.Hashes.CRC32);
        vlocal.MD5 = NormalizeHash(body.Hashes.MD5);
        vlocal.SHA1 = NormalizeHash(body.Hashes.SHA1);
        vlocal.HashSource = (int)body.HashSource;
        vlocal.FileSize = body.FileSize;
        vlocal.DateTimeUpdated = body.DateUpdated ?? now;
        vlocal.DateTimeCreated = body.DateCreated ?? vlocal.DateTimeCreated;
        vlocal.DateTimeImported = body.DateImported ?? vlocal.DateTimeImported;
        if (!string.IsNullOrWhiteSpace(relativePath))
            vlocal.FileName = Path.GetFileName(relativePath);

        RepoFactory.VideoLocal.Save(vlocal, true);

        if (place == null)
        {
            return ValidationProblem("Unable to resolve or create a file location.");
        }

        if (place.VideoLocalID == 0)
        {
            if (importFolder == null)
            {
                importFolder = RepoFactory.ImportFolder.GetByID(place.ImportFolderID);
                if (importFolder == null)
                    return ValidationProblem("Unable to resolve the import folder for the file location.");
            }

            place.VideoLocalID = vlocal.VideoLocalID;
            place.ImportFolderType = importFolder.ImportFolderType;
        }

        RepoFactory.VideoLocalPlace.Save(place);

        SaveFileNameHash(Path.GetFileName(place.FilePath), vlocal, body.DateUpdated ?? now);

        return Ok(new FakeHash.Result
        {
            FileID = vlocal.VideoLocalID,
            FileLocationID = place.VideoLocal_Place_ID
        });
    }

    private static bool TryGetRelativePath(SVR_ImportFolder importFolder, string filePath, out string relativePath, out string errorMessage)
    {
        relativePath = string.Empty;
        errorMessage = string.Empty;

        var importRoot = Path.GetFullPath(importFolder.ImportFolderLocation);
        var fullPath = Path.IsPathRooted(filePath)
            ? Path.GetFullPath(filePath)
            : Path.GetFullPath(Path.Combine(importRoot, filePath));

        if (!fullPath.StartsWith(importRoot, StringComparison.OrdinalIgnoreCase))
        {
            errorMessage = "The provided filePath is not within the given import folder.";
            return false;
        }

        relativePath = Path.GetRelativePath(importRoot, fullPath);
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            errorMessage = "The provided filePath resolved to an empty path.";
            return false;
        }

        return true;
    }

    private static string NormalizeHash(string value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToUpperInvariant();

    private static void SaveFileNameHash(string fileName, SVR_VideoLocal vlocal, DateTime timestamp)
    {
        if (string.IsNullOrWhiteSpace(fileName) || vlocal == null || string.IsNullOrWhiteSpace(vlocal.Hash))
            return;

        var existing = RepoFactory.FileNameHash.GetByFileNameAndSize(fileName, vlocal.FileSize);
        if (existing is { Count: > 1 })
            RepoFactory.FileNameHash.Delete(existing);

        var record = existing is { Count: 1 } ? existing[0] : new Shoko.Models.Server.FileNameHash();
        record.FileName = fileName;
        record.FileSize = vlocal.FileSize;
        record.Hash = vlocal.Hash;
        record.DateTimeUpdated = timestamp;
        RepoFactory.FileNameHash.Save(record);
    }
}
