using System;
using System.Collections.Generic;
using UnityEngine;
using MkLike.Core;
using MkLike.Economy;
using MkLike.Utils;

namespace MkLike.Quest
{
    /// <summary>
    /// 우편함 메일 항목.
    /// </summary>
    [Serializable]
    public class MailItem
    {
        public string id;
        public string sender;
        public string title;
        public string message;
        public CurrencyType rewardType;
        public int rewardAmount;
        /// <summary>만료일 (ISO 8601, UTC)</summary>
        public string expiryDate;
        public bool isRead;
        public bool isClaimed;
    }

    /// <summary>
    /// 우편함 시스템.
    /// 보상 메일 수신/수령, 최대 50개 보관, 만료 자동 제거.
    /// CurrencyManager를 통해 보상을 지급한다.
    /// </summary>
    public class MailboxSystem : MonoBehaviour
    {
        public static MailboxSystem Instance { get; private set; }

        private const int MAX_MAIL_COUNT = 50;

        [SerializeField] private List<MailItem> _mails = new();

        /// <summary>메일 목록 (읽기 전용)</summary>
        public IReadOnlyList<MailItem> Mails => _mails;

        /// <summary>읽지 않은 메일 수</summary>
        public int UnreadCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < _mails.Count; i++)
                {
                    if (!_mails[i].isRead) count++;
                }
                return count;
            }
        }

        /// <summary>수령하지 않은 메일 수</summary>
        public int UnclaimedCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < _mails.Count; i++)
                {
                    if (!_mails[i].isClaimed) count++;
                }
                return count;
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        /// <summary>
        /// 저장 데이터에서 메일 목록을 복원한다.
        /// </summary>
        public void Initialize(List<MailItem> savedMails)
        {
            _mails.Clear();

            if (savedMails != null)
            {
                for (int i = 0; i < savedMails.Count; i++)
                {
                    _mails.Add(savedMails[i]);
                }
            }

            RemoveExpired();

            Debug.Log($"[MailboxSystem] 초기화 완료 — 메일: {_mails.Count}개, 미읽음: {UnreadCount}개");
        }

        /// <summary>
        /// 새 메일을 추가한다.
        /// 보관함이 가득 차면 가장 오래된 수령 완료 메일을 제거한다.
        /// </summary>
        public void AddMail(MailItem mail)
        {
            if (mail == null)
            {
                Debug.LogWarning("[MailboxSystem] null 메일 추가 시도");
                return;
            }

            // ID가 없으면 자동 생성
            if (string.IsNullOrEmpty(mail.id))
            {
                mail.id = Guid.NewGuid().ToString();
            }

            // 보관함이 가득 차면 수령 완료된 가장 오래된 메일 제거
            while (_mails.Count >= MAX_MAIL_COUNT)
            {
                int oldestClaimedIndex = FindOldestClaimedIndex();
                if (oldestClaimedIndex >= 0)
                {
                    _mails.RemoveAt(oldestClaimedIndex);
                }
                else
                {
                    // 수령 완료 메일이 없으면 가장 오래된 메일 제거
                    _mails.RemoveAt(0);
                    Debug.LogWarning("[MailboxSystem] 보관함 가득 참 — 가장 오래된 메일 강제 제거");
                }
            }

            _mails.Add(mail);

            EventBus.Publish(new MailReceivedEvent
            {
                MailId = mail.id,
                Sender = mail.sender,
                Title = mail.title
            });

            Debug.Log($"[MailboxSystem] 메일 수신 — [{mail.sender}] {mail.title}");
        }

        /// <summary>
        /// 간편 메일 추가 메서드.
        /// </summary>
        public void AddMail(string sender, string title, string message, CurrencyType rewardType, int rewardAmount, int expiryDays = 30)
        {
            var mail = new MailItem
            {
                id = Guid.NewGuid().ToString(),
                sender = sender,
                title = title,
                message = message,
                rewardType = rewardType,
                rewardAmount = rewardAmount,
                expiryDate = DateTime.UtcNow.AddDays(expiryDays).ToString("o"),
                isRead = false,
                isClaimed = false
            };

            AddMail(mail);
        }

        /// <summary>
        /// 특정 메일의 보상을 수령한다.
        /// </summary>
        public bool ClaimMail(string mailId)
        {
            if (string.IsNullOrEmpty(mailId)) return false;

            MailItem mail = FindMail(mailId);
            if (mail == null)
            {
                Debug.LogWarning($"[MailboxSystem] 메일을 찾을 수 없음: {mailId}");
                return false;
            }

            if (mail.isClaimed)
            {
                Debug.LogWarning($"[MailboxSystem] 이미 수령한 메일: {mailId}");
                return false;
            }

            // 만료 체크
            if (IsExpired(mail))
            {
                Debug.LogWarning($"[MailboxSystem] 만료된 메일: {mailId}");
                return false;
            }

            // 보상 지급
            if (mail.rewardAmount > 0 && CurrencyManager.Instance != null)
            {
                CurrencyManager.Instance.Add(mail.rewardType, mail.rewardAmount);
            }

            mail.isRead = true;
            mail.isClaimed = true;

            EventBus.Publish(new MailClaimedEvent
            {
                MailId = mailId,
                RewardType = mail.rewardType,
                RewardAmount = mail.rewardAmount
            });

            Debug.Log($"[MailboxSystem] 메일 수령 — {mail.title}: {mail.rewardType} x{mail.rewardAmount}");

            return true;
        }

        /// <summary>
        /// 수령하지 않은 모든 메일의 보상을 수령한다.
        /// </summary>
        /// <returns>수령한 메일 수</returns>
        public int ClaimAll()
        {
            int claimedCount = 0;

            // 임시 리스트로 복사하여 순회 (컬렉션 수정 안전)
            var mailIds = new List<string>(_mails.Count);
            for (int i = 0; i < _mails.Count; i++)
            {
                if (!_mails[i].isClaimed && !IsExpired(_mails[i]))
                {
                    mailIds.Add(_mails[i].id);
                }
            }

            for (int i = 0; i < mailIds.Count; i++)
            {
                if (ClaimMail(mailIds[i]))
                {
                    claimedCount++;
                }
            }

            Debug.Log($"[MailboxSystem] 전체 수령 완료 — {claimedCount}개");

            return claimedCount;
        }

        /// <summary>
        /// 만료된 메일을 제거한다.
        /// </summary>
        public void RemoveExpired()
        {
            int removedCount = 0;

            for (int i = _mails.Count - 1; i >= 0; i--)
            {
                if (IsExpired(_mails[i]))
                {
                    _mails.RemoveAt(i);
                    removedCount++;
                }
            }

            if (removedCount > 0)
            {
                Debug.Log($"[MailboxSystem] 만료 메일 제거 — {removedCount}개");
            }
        }

        /// <summary>
        /// 메일을 읽음 처리한다.
        /// </summary>
        public void MarkAsRead(string mailId)
        {
            MailItem mail = FindMail(mailId);
            if (mail != null)
            {
                mail.isRead = true;
            }
        }

        /// <summary>
        /// 수령 완료된 메일을 모두 삭제한다.
        /// </summary>
        public int RemoveClaimed()
        {
            int removedCount = 0;

            for (int i = _mails.Count - 1; i >= 0; i--)
            {
                if (_mails[i].isClaimed)
                {
                    _mails.RemoveAt(i);
                    removedCount++;
                }
            }

            return removedCount;
        }

        /// <summary>
        /// 저장용 메일 목록을 반환한다.
        /// </summary>
        public List<MailItem> GetMailsForSave()
        {
            return new List<MailItem>(_mails);
        }

        /// <summary>
        /// 메일이 만료되었는지 확인한다.
        /// </summary>
        private bool IsExpired(MailItem mail)
        {
            if (string.IsNullOrEmpty(mail.expiryDate)) return false;

            if (DateTime.TryParse(mail.expiryDate, null,
                System.Globalization.DateTimeStyles.RoundtripKind, out DateTime expiry))
            {
                return DateTime.UtcNow > expiry;
            }

            return false;
        }

        /// <summary>
        /// ID로 메일을 찾는다.
        /// </summary>
        private MailItem FindMail(string mailId)
        {
            for (int i = 0; i < _mails.Count; i++)
            {
                if (string.Equals(_mails[i].id, mailId, StringComparison.Ordinal))
                {
                    return _mails[i];
                }
            }
            return null;
        }

        /// <summary>
        /// 수령 완료된 가장 오래된 메일의 인덱스를 반환한다.
        /// </summary>
        private int FindOldestClaimedIndex()
        {
            for (int i = 0; i < _mails.Count; i++)
            {
                if (_mails[i].isClaimed) return i;
            }
            return -1;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}
