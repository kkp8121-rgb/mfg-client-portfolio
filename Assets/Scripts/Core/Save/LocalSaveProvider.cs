using System;
using System.IO;
using UnityEngine;

namespace MkLike.Core.Save
{
    /// <summary>
    /// 로컬 파일 시스템 기반 세이브 프로바이더.
    /// Application.persistentDataPath에 JSON 파일로 저장/로드한다.
    /// 저장 시 회전 백업(.bak, .bak2)을 생성하며, 크기 급감 시 별도 보존 파일(.bak.corruption.*)을 남긴다.
    /// 로드 실패 시 최신 백업으로 자동 폴백한다.
    /// </summary>
    public class LocalSaveProvider : ISaveProvider
    {
        private const string SaveFileName = "save.json";
        private const string BackupExt = ".bak";
        private const string BackupExt2 = ".bak2";

        /// <summary>정상 세이브로 간주할 최소 바이트 (이보다 작으면 크기 비교를 건너뜀).</summary>
        private const long MIN_SIZE_FOR_GUARD = 10_000;

        /// <summary>신규 저장 크기가 기존의 이 비율 미만이면 손상으로 간주하여 별도 보존.</summary>
        private const float CORRUPTION_RATIO = 0.5f;

        private string FilePath => Path.Combine(Application.persistentDataPath, SaveFileName);
        private string BackupPath => FilePath + BackupExt;
        private string Backup2Path => FilePath + BackupExt2;

        public void Save(SaveData data)
        {
            string json = JsonUtility.ToJson(data, true);
            string path = FilePath;
            int newSize = System.Text.Encoding.UTF8.GetByteCount(json);

            if (File.Exists(path))
            {
                long oldSize;
                try { oldSize = new FileInfo(path).Length; }
                catch { oldSize = 0; }

                // 크기 급감 감지 → 별도 보존 (개발자 수동 복구용)
                if (oldSize > MIN_SIZE_FOR_GUARD && newSize < oldSize * CORRUPTION_RATIO)
                {
                    string corruptPath = $"{path}.bak.corruption.{DateTime.Now:yyyyMMdd_HHmmss}";
                    try
                    {
                        File.Copy(path, corruptPath, true);
                        Debug.LogError($"[LocalSaveProvider] 세이브 크기 급감 감지! old={oldSize}B → new={newSize}B. 기존 파일 보존: {corruptPath}");
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"[LocalSaveProvider] 손상 백업 실패: {e.Message}");
                    }
                }

                // 회전 백업: bak2 ← bak ← current
                try
                {
                    if (File.Exists(BackupPath))
                        File.Copy(BackupPath, Backup2Path, true);
                    File.Copy(path, BackupPath, true);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[LocalSaveProvider] 정기 백업 실패: {e.Message}");
                }
            }

            File.WriteAllText(path, json);
            Debug.Log($"[LocalSaveProvider] 세이브 완료: {path} ({newSize}B)");
        }

        public SaveData Load()
        {
            SaveData data = TryLoad(FilePath);
            if (data != null) return data;

            // 폴백 1: .bak
            data = TryLoad(BackupPath);
            if (data != null)
            {
                Debug.LogWarning($"[LocalSaveProvider] 기본 파일 로드 실패 → {BackupPath}에서 복원");
                return data;
            }

            // 폴백 2: .bak2
            data = TryLoad(Backup2Path);
            if (data != null)
            {
                Debug.LogWarning($"[LocalSaveProvider] 백업 #1 실패 → {Backup2Path}에서 복원");
                return data;
            }

            Debug.LogWarning("[LocalSaveProvider] 세이브 파일 + 백업 모두 로드 실패");
            return null;
        }

        private SaveData TryLoad(string path)
        {
            if (!File.Exists(path)) return null;
            try
            {
                string json = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(json)) return null;
                SaveData d = JsonUtility.FromJson<SaveData>(json);
                if (d != null) Debug.Log($"[LocalSaveProvider] 로드 완료: {path}");
                return d;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[LocalSaveProvider] 로드 실패 ({path}): {e.Message}");
                return null;
            }
        }

        public bool HasSave()
        {
            return File.Exists(FilePath) || File.Exists(BackupPath) || File.Exists(Backup2Path);
        }

        public void Delete()
        {
            SafeDelete(FilePath);
            SafeDelete(BackupPath);
            SafeDelete(Backup2Path);
        }

        private void SafeDelete(string path)
        {
            if (!File.Exists(path)) return;
            try
            {
                File.Delete(path);
                Debug.Log($"[LocalSaveProvider] 삭제: {path}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[LocalSaveProvider] 삭제 실패 ({path}): {e.Message}");
            }
        }
    }
}
