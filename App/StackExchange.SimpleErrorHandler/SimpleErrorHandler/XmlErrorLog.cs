using System;
using System.Collections;
using System.IO;
using System.Text.RegularExpressions;
using System.Web;
using System.Xml;

namespace SimpleErrorHandler
{
    /// <summary>
    /// An <see cref="ErrorLog"/> implementation that uses XML files stored on disk as its backing store.
    /// </summary>
    public class XmlErrorLog : ErrorLog
    {
        private string _logPath;
        private int _maxFiles = 200;
        /// <summary>
        /// When set in config, any new exceptions will be compared to existing exceptions within this time window.  If new exceptions match, they will be discarded.
        /// Useful for when a deluge of errors comes down upon your head.
        /// </summary>
        private TimeSpan? _ignoreSimilarExceptionsThreshold;

        public string LogPath
        {
            get { return _logPath; }
            set
            {
                if (value.StartsWith(@"~\"))
                {
                    _logPath = AppDomain.CurrentDomain.GetData("APPBASE").ToString() + value.Substring(2);
                }
                else
                {
                    _logPath = value;
                }
            }
        }

        public override bool DeleteError(string id)
        {
            FileInfo f;
            if (!TryGetErrorFile(id, out f))
                return false;

            // remove the read-only before deletion
            if (f.IsReadOnly)
                f.Attributes ^= FileAttributes.ReadOnly;

            f.Delete();
            return true;
        }

        public override bool ProtectError(string id)
        {
            FileInfo f;
            if (!TryGetErrorFile(id, out f))
                return false;

            f.Attributes |= FileAttributes.ReadOnly;
            return true;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ErrorLog"/> class
        /// using a dictionary of configured settings.
        /// </summary>
        public XmlErrorLog(IDictionary config)
        {
            if (config["LogPath"] != null)
            {
                LogPath = (string)config["LogPath"];
            }
            else
            {
                throw new Exception("Log Path is missing for the XML error log.");
            }

            if (config["MaxFiles"] != null)
            {
                _maxFiles = Convert.ToInt32(config["MaxFiles"]);
            }

            if (config["IgnoreSimilarExceptionsThreshold"] != null)
            {
                // the config file value will be a positive time span, but we'll be subtracting this value from "Now" - negate it
                _ignoreSimilarExceptionsThreshold = TimeSpan.Parse(config["IgnoreSimilarExceptionsThreshold"].ToString()).Negate();
            }

        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ErrorLog"/> class to use a specific path to store/load XML files.
        /// </summary>
        public XmlErrorLog(string logPath)
        {
            LogPath = logPath;
        }

        /// <summary>
        /// Gets the name of this error log implementation.
        /// </summary>
        public override string Name
        {
            get { return "Xml File Error Log"; }
        }

        /// <summary>
        /// Logs an error to the database.
        /// </summary>
        /// <remarks>
        /// Logs an error as a single XML file stored in a folder. XML files are named with a
        /// sortable date and a unique identifier. Currently the XML files are stored indefinately.
        /// As they are stored as files, they may be managed using standard scheduled jobs.
        /// </remarks>
        public override void Log(Error error)
        {
            // will allow fast comparisons of messages to see if we can ignore an incoming exception
            string messageHash = error.Detail;
            messageHash = messageHash.HasValue() ? messageHash.GetHashCode().ToString() : "no-stack-trace";
            Error original;

            // before we persist 'error', see if there are any existing errors that it could be a duplicate of
            if (_ignoreSimilarExceptionsThreshold.HasValue && TryFindOriginalError(error, messageHash, out original))
            {
                // just update the existing file after incrementing its "duplicate count"
                original.DuplicateCount = original.DuplicateCount.GetValueOrDefault(0) + 1;
                UpdateError(original);
            }
            else
            {
                LogNewError(error, messageHash);
            }
        }

        private void UpdateError(Error error)
        {
            FileInfo f;
            if (!TryGetErrorFile(error.Id, out f))
                throw new ArgumentOutOfRangeException("Unable to find a file for error with Id = " + error.Id);

            using (var stream = f.OpenWrite())
            using (var writer = new StreamWriter(stream))
            {
                LogError(error, writer);
            }
        }

        private void LogNewError(Error error, string messageHash)
        {
            error.Id = FriendlyGuid(Guid.NewGuid());
            string timeStamp = DateTime.Now.ToString("u").Replace(":", "").Replace(" ", "");
            string fileName = string.Format(@"{0}\error-{1}-{2}-{3}.xml", _logPath, timeStamp, messageHash, error.Id);

            FileInfo outfile = new FileInfo(fileName);
            using (StreamWriter outstream = outfile.CreateText())
            {
                LogError(error, outstream);
            }

            // we added a new file, so clean up old smack over our max errors limit
            RemoveOldErrors();
        }

        private void LogError(Error error, StreamWriter outstream)
        {
            using (XmlTextWriter w = new XmlTextWriter(outstream))
            {
                w.Formatting = Formatting.Indented;

                w.WriteStartElement("error");
                error.ToXml(w);
                w.WriteEndElement();
                w.Flush();
            }
        }

        /// <summary>
        /// Answers the older exception that 'possibleDuplicate' matches, returning null if no match is found.
        /// </summary>
        private bool TryFindOriginalError(SimpleErrorHandler.Error possibleDuplicate, string messageHash, out SimpleErrorHandler.Error original)
        {
            string[] files = Directory.GetFiles(LogPath);

            if (files.Length > 0)
            {
                var earliestDate = DateTime.Now.Add(_ignoreSimilarExceptionsThreshold.Value);

                // order by newest
                Array.Sort(files);
                Array.Reverse(files);

                foreach (var filename in files)
                {
                    if (File.GetCreationTime(filename) >= earliestDate)
                    {
                        var match = Regex.Match(filename, @"error[-\d]+Z-(?<hashCode>((?<!\d)-|\d)+)-(?<id>.+)\.xml", RegexOptions.IgnoreCase);
                        if (match.Success)
                        {
                            var existingHash = match.Groups["hashCode"].Value;
                            if (messageHash.Equals(existingHash))
                            {
                                original = GetError(match.Groups["id"].Value).Error;
                                return true;
                            }
                        }
                    }
                    else
                        break; // no other files are newer, no use checking
                }
            }

            original = null;
            return false;
        }


        private string FriendlyGuid(Guid g)
        {
            string s = Convert.ToBase64String(g.ToByteArray());
            return s
              .Replace("/", "")
              .Replace("+", "")
              .Replace("=", "");
        }

        private void RemoveOldErrors()
        {
            string[] fileList = Directory.GetFiles(LogPath, "error*.*");

            // we'll start deleting once we're over the max
            if (fileList.Length <= _maxFiles) return;

            // file name contains timestamp - sort by creation date, ascending
            Array.Sort(fileList);

            // we'll remove any errors with index less than this upper bound
            int upperBound = fileList.Length - _maxFiles;

            for (int i = 0; i < upperBound && i < fileList.Length; i++)
            {
                var file = new FileInfo(fileList[i]);

                // have we protected this error from deletion?
                if ((file.Attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                {
                    // we'll skip this error file and raise our search bounds up one
                    upperBound++;
                }
                else
                {
                    file.Delete();
                }
            }
        }

        /// <summary>
        /// Returns a page of errors from the folder in descending order of logged time as defined by the sortable filenames.
        /// </summary>
        public override int GetErrors(int pageIndex, int pageSize, IList errorEntryList)
        {
            if (pageIndex < 0) pageIndex = 0;
            if (pageSize < 0) pageSize = 25;

            string[] fileList = Directory.GetFiles(LogPath, "*.xml");

            if (fileList.Length < 1) return 0;

            Array.Sort(fileList);
            Array.Reverse(fileList);

            int currentItem = pageIndex * pageSize;
            int lastItem = (currentItem + pageSize < fileList.Length) ? currentItem + pageSize : fileList.Length;

            for (int i = currentItem; i < lastItem; i++)
            {
                FileInfo f = new FileInfo(fileList[i]);
                FileStream s = f.OpenRead();
                XmlTextReader r = new XmlTextReader(s);

                try
                {
                    while (r.IsStartElement("error"))
                    {
                        SimpleErrorHandler.Error error = new SimpleErrorHandler.Error();
                        error.FromXml(r);
                        error.IsProtected = f.IsReadOnly; // have we "protected" this file from deletion?
                        errorEntryList.Add(new ErrorLogEntry(this, error.Id, error));
                    }
                }
                finally
                {
                    r.Close();
                }

            }

            return fileList.Length;
        }

        /// <summary>
        /// Returns the specified error from the filesystem, or throws an exception if it does not exist.
        /// </summary>
        public override ErrorLogEntry GetError(string id)
        {
            string[] fileList = Directory.GetFiles(LogPath, string.Format("*{0}.xml", id));

            if (fileList.Length < 1)
                throw new Exception(string.Format("Can't locate error file for errorId {0}", id));

            FileInfo f = new FileInfo(fileList[0]);
            FileStream s = f.OpenRead();
            XmlTextReader r = new XmlTextReader(s);
            SimpleErrorHandler.Error error = new SimpleErrorHandler.Error();
            error.FromXml(r);
            r.Close();
            return new ErrorLogEntry(this, id, error);
        }

        private bool TryGetErrorFile(string id, out FileInfo file)
        {
            string[] fileList = Directory.GetFiles(LogPath, string.Format("*{0}.xml", id));

            if (fileList.Length != 1)
            {
                file = null;
                return false;
            }

            file = new FileInfo(fileList[0]);
            return true;
        }
    }
}