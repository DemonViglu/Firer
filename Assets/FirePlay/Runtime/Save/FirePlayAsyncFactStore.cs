using System;
using System.Collections.Generic;
using System.IO;
using DemonViglu.FirePlay.Activity;
using UnityEngine;

namespace DemonViglu.FirePlay.Save
{
    public enum FirePlayAsyncFactKind
    {
        ActivityInteraction,
        Campfire,
        SmallFire,
        WorldTree
    }

    [Serializable]
    public sealed class FirePlayAsyncFactRecord
    {
        public FirePlayAsyncFactKind kind;
        public string actorId;
        public string eventId;
        public long occurredAtUnixMs;
        public uint revision;
        public ActivityTargetKind targetKind;
        public string targetId;
        public string activityId;
        public string actionId;
        public string payload;

        public ActivityFactMetadata Metadata => new(
            actorId,
            eventId,
            occurredAtUnixMs,
            revision);

        public static FirePlayAsyncFactRecord FromActivity(ActivityFactDto fact) => new()
        {
            kind = FirePlayAsyncFactKind.ActivityInteraction,
            actorId = fact.Metadata.ActorId,
            eventId = fact.Metadata.EventId,
            occurredAtUnixMs = fact.Metadata.OccurredAtUnixMs,
            revision = fact.Metadata.FactRevision,
            targetKind = fact.TargetKind,
            targetId = fact.TargetId,
            activityId = fact.ActivityId,
            actionId = fact.ActionId,
            payload = fact.Payload
        };
    }

    [Serializable]
    internal sealed class FirePlayAsyncFactRecordList
    {
        public List<FirePlayAsyncFactRecord> records = new();
    }

    /// <summary>
    /// Backend-neutral persistence boundary. A remote adapter can implement
    /// the same interface later without changing Activity or world logic.
    /// </summary>
    public interface IAsyncInteractionFactStore
    {
        bool AppendActivity(ActivityFactDto fact, out string reason);
        bool AppendWorld(
            FirePlayAsyncFactKind kind,
            ActivityFactMetadata metadata,
            ActivityTargetReference target,
            string actionId,
            string payload,
            out string reason);
        IReadOnlyList<FirePlayAsyncFactRecord> ReadAll();
    }

    /// <summary>
    /// Small local adapter for prototyping async social traces. It stores
    /// facts, not a second gameplay state machine, and rejects duplicate IDs.
    /// </summary>
    public sealed class LocalAsyncInteractionFactStore : IAsyncInteractionFactStore
    {
        private const string FileName = "fireplay-async-facts.json";
        private const int MaximumStableIdLength = 128;
        private const int MaximumPayloadLength = 512;
        private readonly List<FirePlayAsyncFactRecord> _records = new();
        private readonly HashSet<string> _eventIds = new(StringComparer.Ordinal);
        private bool _loaded;

        public string SavePath => Path.Combine(Application.persistentDataPath, FileName);

        public bool AppendActivity(ActivityFactDto fact, out string reason)
        {
            if (fact.Kind != ActivityNetworkFactKind.InteractionOccurred
                && fact.Kind != ActivityNetworkFactKind.SocialInteractionOccurred)
            {
                reason = "Only activity interaction facts are async social records";
                return false;
            }
            if (!IsValidStableText(fact.ActivityId)
                || !IsValidStableText(fact.ActionId)
                || !IsValidTargetShape(fact.TargetKind, fact.TargetId)
                || (fact.Payload?.Length ?? 0) > MaximumPayloadLength)
            {
                reason = "Async activity fact contains invalid stable data";
                return false;
            }
            return AppendRecord(FirePlayAsyncFactRecord.FromActivity(fact), out reason);
        }

        public bool AppendWorld(
            FirePlayAsyncFactKind kind,
            ActivityFactMetadata metadata,
            ActivityTargetReference target,
            string actionId,
            string payload,
            out string reason)
        {
            if (kind != FirePlayAsyncFactKind.Campfire
                && kind != FirePlayAsyncFactKind.SmallFire
                && kind != FirePlayAsyncFactKind.WorldTree)
            {
                reason = "Invalid async world fact kind";
                return false;
            }
            if (!target.IsValid
                || !IsValidStableText(target.Id)
                || !IsValidStableText(actionId)
                || (payload?.Length ?? 0) > MaximumPayloadLength)
            {
                reason = "Async world fact contains invalid stable data";
                return false;
            }
            var record = new FirePlayAsyncFactRecord
            {
                kind = kind,
                actorId = metadata.ActorId,
                eventId = metadata.EventId,
                occurredAtUnixMs = metadata.OccurredAtUnixMs,
                revision = metadata.FactRevision,
                targetKind = target.Kind,
                targetId = target.Id,
                actionId = actionId ?? string.Empty,
                payload = payload ?? string.Empty
            };
            return AppendRecord(record, out reason);
        }

        public IReadOnlyList<FirePlayAsyncFactRecord> ReadAll()
        {
            EnsureLoaded();
            return _records.ToArray();
        }

        private bool AppendRecord(FirePlayAsyncFactRecord record, out string reason)
        {
            EnsureLoaded();
            if (!IsValidRecord(record))
            {
                reason = "Async fact record is invalid";
                return false;
            }
            if (!_eventIds.Add(record.eventId))
            {
                reason = "Async fact EventId was already persisted";
                return false;
            }
            _records.Add(record);
            try
            {
                var wrapper = new FirePlayAsyncFactRecordList { records = _records };
                File.WriteAllText(SavePath, JsonUtility.ToJson(wrapper, true));
                reason = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                _records.RemoveAt(_records.Count - 1);
                _eventIds.Remove(record.eventId);
                reason = exception.Message;
                return false;
            }
        }

        private void EnsureLoaded()
        {
            if (_loaded)
                return;
            _loaded = true;
            if (!File.Exists(SavePath))
                return;
            try
            {
                var wrapper = JsonUtility.FromJson<FirePlayAsyncFactRecordList>(
                    File.ReadAllText(SavePath));
                if (wrapper?.records == null)
                    return;
                foreach (var record in wrapper.records)
                {
                    if (!IsValidRecord(record) || !_eventIds.Add(record.eventId))
                        continue;
                    _records.Add(record);
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[LocalAsyncInteractionFactStore] Load failed: {exception.Message}");
            }
        }

        private static bool IsValidRecord(FirePlayAsyncFactRecord record)
        {
            if (record == null
                || !record.Metadata.IsValid
                || !IsValidStableText(record.actorId)
                || !IsValidStableText(record.eventId)
                || !IsValidStableText(record.actionId)
                || (record.payload?.Length ?? 0) > MaximumPayloadLength
                || !Enum.IsDefined(typeof(FirePlayAsyncFactKind), record.kind))
            {
                return false;
            }

            if (record.kind == FirePlayAsyncFactKind.ActivityInteraction)
            {
                return IsValidStableText(record.activityId)
                    && IsValidTargetShape(record.targetKind, record.targetId);
            }

            return string.IsNullOrWhiteSpace(record.activityId)
                && record.targetKind != ActivityTargetKind.None
                && IsValidTargetShape(record.targetKind, record.targetId);
        }

        private static bool IsValidTargetShape(ActivityTargetKind kind, string id)
        {
            if (!Enum.IsDefined(typeof(ActivityTargetKind), kind))
                return false;
            return kind == ActivityTargetKind.None
                ? string.IsNullOrWhiteSpace(id)
                : IsValidStableText(id);
        }

        private static bool IsValidStableText(string value) =>
            !string.IsNullOrWhiteSpace(value)
            && value.Length <= MaximumStableIdLength;
    }
}
