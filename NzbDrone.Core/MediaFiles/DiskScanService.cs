﻿using System.Collections.Generic;
using System.IO;
using System.Linq;
using NLog;
using NzbDrone.Common;
using NzbDrone.Common.Messaging;
using NzbDrone.Core.MediaFiles.Commands;
using NzbDrone.Core.MediaFiles.EpisodeImport;
using NzbDrone.Core.Tv;
using NzbDrone.Core.Tv.Events;

namespace NzbDrone.Core.MediaFiles
{
    public interface IDiskScanService
    {
        string[] GetVideoFiles(string path, bool allDirectories = true);
    }

    public class DiskScanService :
        IDiskScanService,
        IHandle<SeriesUpdatedEvent>
    {
        private readonly HashSet<string> _mediaExtensions;

        private const string EXTENSIONS =
            //XBMC
            ".m4v .3gp .nsv .ts .ty .strm .rm .rmvb .m3u .ifo .mov .qt .divx .xvid .bivx .vob .nrg .img " +
            ".iso .pva .wmv .asf .asx .ogm .m2v .avi .bin .dat .dvr-ms .mpg .mpeg .mp4 .mkv .avc .vp3 " +
            ".svq3 .nuv .viv .dv .fli .flv .wpl " +
            //Other
            ".m2ts";

        private readonly IDiskProvider _diskProvider;
        private readonly IMakeImportDecision _importDecisionMaker;
        private readonly IImportApprovedEpisodes _importApprovedEpisodes;
        private readonly IMessageAggregator _messageAggregator;
        private readonly Logger _logger;

        public DiskScanService(IDiskProvider diskProvider,
                                IMakeImportDecision importDecisionMaker,
                                IImportApprovedEpisodes importApprovedEpisodes,
                                IMessageAggregator messageAggregator, Logger logger)
        {
            _diskProvider = diskProvider;
            _importDecisionMaker = importDecisionMaker;
            _importApprovedEpisodes = importApprovedEpisodes;
            _messageAggregator = messageAggregator;
            _logger = logger;

            _mediaExtensions = new HashSet<string>(EXTENSIONS.Split(' ').Select(c => c.ToLower()));
        }

        private void Scan(Series series)
        {
            _messageAggregator.PublishCommand(new CleanMediaFileDb(series.Id));

            if (!_diskProvider.FolderExists(series.Path))
            {
                _logger.Debug("Series folder doesn't exist: {0}", series.Path);
                return;
            }

            var mediaFileList = GetVideoFiles(series.Path);

            var decisions = _importDecisionMaker.GetImportDecisions(mediaFileList, series, false);
            _importApprovedEpisodes.Import(decisions);
        }

        public string[] GetVideoFiles(string path, bool allDirectories = true)
        {
            _logger.Debug("Scanning '{0}' for video files", path);

            var searchOption = allDirectories ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var filesOnDisk = _diskProvider.GetFiles(path, searchOption);

            var mediaFileList = filesOnDisk.Where(c => _mediaExtensions.Contains(Path.GetExtension(c).ToLower())).ToList();

            _logger.Trace("{0} video files were found in {1}", mediaFileList.Count, path);
            return mediaFileList.ToArray();
        }

        public void Handle(SeriesUpdatedEvent message)
        {
            Scan(message.Series);
        }
    }
}