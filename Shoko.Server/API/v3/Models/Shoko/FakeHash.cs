using System;
using System.ComponentModel.DataAnnotations;
using Shoko.Server.Server;

namespace Shoko.Server.API.v3.Models.Shoko;

public class FakeHash
{
    public class Hashes
    {
        /// <summary>
        /// ED2K hex-encoded hash.
        /// </summary>
        [Required, Length(32, 32)]
        public string ED2K { get; set; }

        /// <summary>
        /// CRC32 hex-encoded hash.
        /// </summary>
        [Required, Length(8, 8)]
        public string CRC32 { get; set; }

        /// <summary>
        /// MD5 hex-encoded hash.
        /// </summary>
        [Required, Length(32, 32)]
        public string MD5 { get; set; }

        /// <summary>
        /// SHA1 hex-encoded hash.
        /// </summary>
        [Required, Length(40, 40)]
        public string SHA1 { get; set; }
    }

    public class Body
    {
        /// <summary>
        /// The file id to update.
        /// </summary>
        [Range(1, int.MaxValue)]
        public int? FileID { get; set; }

        /// <summary>
        /// The import folder id.
        /// </summary>
        [Range(1, int.MaxValue)]
        public int? ImportFolderID { get; set; }

        /// <summary>
        /// The relative or absolute path to the file within the import folder.
        /// </summary>
        public string FilePath { get; set; }

        /// <summary>
        /// The file size in bytes.
        /// </summary>
        [Range(1L, long.MaxValue)]
        public long FileSize { get; set; }

        /// <summary>
        /// The hashes to store for the file.
        /// </summary>
        [Required]
        public Hashes Hashes { get; set; }

        /// <summary>
        /// The hash source to store.
        /// </summary>
        public HashSource HashSource { get; set; } = HashSource.DirectHash;

        /// <summary>
        /// The created date to store.
        /// </summary>
        public DateTime? DateCreated { get; set; }

        /// <summary>
        /// The updated date to store.
        /// </summary>
        public DateTime? DateUpdated { get; set; }

        /// <summary>
        /// The imported date to store.
        /// </summary>
        public DateTime? DateImported { get; set; }
    }

    public class Result
    {
        public int FileID { get; set; }

        public int FileLocationID { get; set; }
    }
}
