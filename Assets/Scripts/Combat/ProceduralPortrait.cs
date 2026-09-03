using UnityEngine;
using MkLike.Core;

namespace MkLike.Combat
{
    /// <summary>
    /// 프로시져럴 초상화 생성기.
    /// 직업(JobType 기반), 몬스터용 초상화를 코드로 생성한다.
    /// 아이콘 에셋이 없을 때 fallback으로 사용: 직업/타입 색상 원형 + 이니셜 텍스트.
    /// 생성된 Texture2D를 Sprite로 변환하여 UI Icon에 사용.
    /// </summary>
    public static class ProceduralPortrait
    {
        private const int PORTRAIT_SIZE = 64;

        #region 직업 색상

        /// <summary>
        /// JobType 기반 색상을 반환한다.
        /// </summary>
        public static Color GetJobColor(JobType job)
        {
            return job switch
            {
                JobType.Warrior => new Color(0.8f, 0.25f, 0.2f),   // 붉은색
                JobType.Archer => new Color(0.2f, 0.7f, 0.3f),     // 녹색
                JobType.Mage => new Color(0.3f, 0.35f, 0.85f),     // 파란색
                _ => new Color(0.5f, 0.5f, 0.5f)
            };
        }

        #endregion

        #region 몬스터 초상화

        /// <summary>
        /// 몬스터용 프로시져럴 초상화. 빨간 배경 + 이니셜.
        /// </summary>
        public static Sprite GetMonsterPortrait(string displayName)
        {
            Color bgColor = new Color(0.5f, 0.15f, 0.15f);
            string initial = GetInitial(displayName);
            return GenerateCirclePortrait(bgColor, Color.white, initial);
        }

        #endregion

        #region 코어 생성

        /// <summary>
        /// 원형 배경 + 이니셜 1글자 초상화를 생성한다.
        /// </summary>
        private static Sprite GenerateCirclePortrait(Color bgColor, Color textColor, string initial)
        {
            int size = PORTRAIT_SIZE;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;

            float center = size * 0.5f;
            float radius = center - 2f;
            float borderRadius = center - 1f;

            // 원형 배경 + 테두리 그리기
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));

                    if (dist <= radius)
                    {
                        // 내부: 그라데이션 (위쪽이 약간 밝음)
                        float gradientFactor = 1f + (y - center) / (float)size * 0.3f;
                        Color pixel = bgColor * gradientFactor;
                        pixel.a = 1f;
                        tex.SetPixel(x, y, pixel);
                    }
                    else if (dist <= borderRadius)
                    {
                        // 테두리: 밝은 색상
                        Color border = Color.Lerp(bgColor, Color.white, 0.4f);
                        border.a = 1f;
                        tex.SetPixel(x, y, border);
                    }
                    else
                    {
                        // 외부: 투명
                        tex.SetPixel(x, y, Color.clear);
                    }
                }
            }

            // 이니셜 글자 렌더링 (간단한 비트맵 폰트)
            if (!string.IsNullOrEmpty(initial))
                DrawInitial(tex, initial[0], textColor, size);

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), Vector2.one * 0.5f, 100f);
        }

        /// <summary>
        /// 간단한 비트맵 방식으로 중앙에 글자를 그린다.
        /// 5x7 비트맵 폰트 패턴 사용 (영문 대문자 + 한글 일부).
        /// </summary>
        private static void DrawInitial(Texture2D tex, char ch, Color color, int size)
        {
            // 간단한 도트 패턴으로 글자 그리기 (중앙 배치)
            bool[,] pattern = GetCharPattern(ch);
            if (pattern == null) return;

            int patW = pattern.GetLength(1);
            int patH = pattern.GetLength(0);
            int scale = size / 12;  // 스케일 팩터
            if (scale < 1) scale = 1;

            int startX = (size - patW * scale) / 2;
            int startY = (size - patH * scale) / 2;

            for (int py = 0; py < patH; py++)
            {
                for (int px = 0; px < patW; px++)
                {
                    if (!pattern[py, px]) continue;

                    for (int sy = 0; sy < scale; sy++)
                    {
                        for (int sx = 0; sx < scale; sx++)
                        {
                            int tx = startX + px * scale + sx;
                            int ty = startY + (patH - 1 - py) * scale + sy;
                            if (tx >= 0 && tx < size && ty >= 0 && ty < size)
                                tex.SetPixel(tx, ty, color);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 5x7 비트맵 패턴을 반환한다. 알파벳 일부 + 기본 심볼.
        /// </summary>
        private static bool[,] GetCharPattern(char ch)
        {
            // 대문자로 변환
            ch = char.ToUpper(ch);

            return ch switch
            {
                'W' => new bool[,] {
                    {true,false,false,false,true},
                    {true,false,false,false,true},
                    {true,false,true,false,true},
                    {true,false,true,false,true},
                    {true,true,false,true,true},
                    {true,true,false,true,true},
                    {true,false,false,false,true}
                },
                'A' => new bool[,] {
                    {false,true,true,true,false},
                    {true,false,false,false,true},
                    {true,false,false,false,true},
                    {true,true,true,true,true},
                    {true,false,false,false,true},
                    {true,false,false,false,true},
                    {true,false,false,false,true}
                },
                'M' => new bool[,] {
                    {true,false,false,false,true},
                    {true,true,false,true,true},
                    {true,false,true,false,true},
                    {true,false,true,false,true},
                    {true,false,false,false,true},
                    {true,false,false,false,true},
                    {true,false,false,false,true}
                },
                'K' => new bool[,] {
                    {true,false,false,true,false},
                    {true,false,true,false,false},
                    {true,true,false,false,false},
                    {true,true,false,false,false},
                    {true,false,true,false,false},
                    {true,false,false,true,false},
                    {true,false,false,false,true}
                },
                'S' => new bool[,] {
                    {false,true,true,true,false},
                    {true,false,false,false,true},
                    {true,false,false,false,false},
                    {false,true,true,true,false},
                    {false,false,false,false,true},
                    {true,false,false,false,true},
                    {false,true,true,true,false}
                },
                'T' => new bool[,] {
                    {true,true,true,true,true},
                    {false,false,true,false,false},
                    {false,false,true,false,false},
                    {false,false,true,false,false},
                    {false,false,true,false,false},
                    {false,false,true,false,false},
                    {false,false,true,false,false}
                },
                'R' => new bool[,] {
                    {true,true,true,true,false},
                    {true,false,false,false,true},
                    {true,false,false,false,true},
                    {true,true,true,true,false},
                    {true,false,true,false,false},
                    {true,false,false,true,false},
                    {true,false,false,false,true}
                },
                'H' => new bool[,] {
                    {true,false,false,false,true},
                    {true,false,false,false,true},
                    {true,false,false,false,true},
                    {true,true,true,true,true},
                    {true,false,false,false,true},
                    {true,false,false,false,true},
                    {true,false,false,false,true}
                },
                'D' => new bool[,] {
                    {true,true,true,true,false},
                    {true,false,false,false,true},
                    {true,false,false,false,true},
                    {true,false,false,false,true},
                    {true,false,false,false,true},
                    {true,false,false,false,true},
                    {true,true,true,true,false}
                },
                'P' => new bool[,] {
                    {true,true,true,true,false},
                    {true,false,false,false,true},
                    {true,false,false,false,true},
                    {true,true,true,true,false},
                    {true,false,false,false,false},
                    {true,false,false,false,false},
                    {true,false,false,false,false}
                },
                'C' => new bool[,] {
                    {false,true,true,true,false},
                    {true,false,false,false,true},
                    {true,false,false,false,false},
                    {true,false,false,false,false},
                    {true,false,false,false,false},
                    {true,false,false,false,true},
                    {false,true,true,true,false}
                },
                'B' => new bool[,] {
                    {true,true,true,true,false},
                    {true,false,false,false,true},
                    {true,false,false,false,true},
                    {true,true,true,true,false},
                    {true,false,false,false,true},
                    {true,false,false,false,true},
                    {true,true,true,true,false}
                },
                'F' => new bool[,] {
                    {true,true,true,true,true},
                    {true,false,false,false,false},
                    {true,false,false,false,false},
                    {true,true,true,true,false},
                    {true,false,false,false,false},
                    {true,false,false,false,false},
                    {true,false,false,false,false}
                },
                // 기본 심볼: 다이아몬드
                _ => new bool[,] {
                    {false,false,true,false,false},
                    {false,true,false,true,false},
                    {true,false,false,false,true},
                    {false,true,false,true,false},
                    {false,false,true,false,false},
                    {false,false,false,false,false},
                    {false,false,false,false,false}
                }
            };
        }

        #endregion

        #region 유틸리티

        /// <summary>
        /// 표시명에서 이니셜 1글자를 추출한다.
        /// 한글이면 첫 글자, 영문이면 대문자 이니셜.
        /// </summary>
        private static string GetInitial(string name)
        {
            if (string.IsNullOrEmpty(name)) return "?";

            char first = name[0];

            // 한글이면 자음 추출 불가 → 첫 글자 그대로
            if (first >= '가' && first <= '힣')
                return first.ToString();

            // 영문이면 대문자
            return char.ToUpper(first).ToString();
        }

        /// <summary>
        /// 배경색에 대한 대비 텍스트 색상을 반환한다.
        /// </summary>
        private static Color GetContrastColor(Color bg)
        {
            float luminance = 0.299f * bg.r + 0.587f * bg.g + 0.114f * bg.b;
            return luminance > 0.5f ? new Color(0.1f, 0.1f, 0.1f) : Color.white;
        }

        #endregion
    }
}
