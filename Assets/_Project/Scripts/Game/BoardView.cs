using System;
using System.Collections.Generic;
using Liman.Core;
using UnityEngine;

namespace Liman.Game
{
    /// <summary>
    /// Placeholder renderer and input handler made of coloured boxes and TextMesh labels.
    /// It rebuilds the whole board on every change, which is fine at this scale; replace it with
    /// pooled sprites and animations when real art arrives.
    /// </summary>
    public sealed class BoardView : MonoBehaviour
    {
        private const float ContainerWidth = 1f;
        private const float ContainerHeight = 0.5f;
        private const float StackGap = 0.35f;
        private const float StackBaseY = -3.3f;
        private const float DockY = 2.6f;
        private const float DockWidth = 3f;
        private const float DockGap = 0.5f;
        private const float QueueY = 4.1f;
        private const float TitleY = 5.4f;
        private const float ButtonY = -4.7f;
        private const float SlotSize = 0.45f;

        private static readonly Color[] Palette =
        {
            new Color32(231, 76, 60, 255),
            new Color32(52, 152, 219, 255),
            new Color32(241, 196, 15, 255),
            new Color32(46, 204, 113, 255),
            new Color32(155, 89, 182, 255),
            new Color32(230, 126, 34, 255),
            new Color32(26, 188, 156, 255),
            new Color32(236, 64, 122, 255),
        };

        private static readonly Color Background = new Color32(18, 32, 47, 255);
        private static readonly Color Panel = new Color32(58, 80, 105, 255);
        private static readonly Color PanelDark = new Color32(30, 46, 64, 255);
        private static readonly Color HintColor = new Color32(255, 214, 10, 255);
        private static readonly Color TextColor = Color.white;
        private static readonly Color DimTextColor = new Color(1f, 1f, 1f, 0.55f);

        private readonly struct HitArea
        {
            public readonly Rect Rect;
            public readonly Action OnTap;

            public HitArea(Rect rect, Action onTap)
            {
                Rect = rect;
                OnTap = onTap;
            }
        }

        private readonly List<HitArea> _hitAreas = new List<HitArea>();
        private GameController _game;
        private Camera _camera;
        private Sprite _square;
        private Font _font;
        private Transform _content;
        private int _selectedStack = -1;
        private Move? _hint;
        private string _overlayText;
        private Action _overlayTap;

        public void Bind(GameController game)
        {
            _game = game;
            _game.StateChanged += OnStateChanged;
            _game.LevelCompleted += OnLevelCompleted;
            _game.LevelStuck += OnLevelStuck;
        }

        private void Awake()
        {
            _camera = Camera.main;
            if (_camera == null)
            {
                var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
                _camera = cameraObject.AddComponent<Camera>();
            }
            _camera.orthographic = true;
            _camera.transform.SetPositionAndRotation(new Vector3(0f, 0f, -10f), Quaternion.identity);
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = Background;

            var texture = new Texture2D(4, 4, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            var pixels = new Color[16];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
            texture.SetPixels(pixels);
            texture.Apply();
            _square = Sprite.Create(texture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 4f);
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        private void OnDestroy()
        {
            if (_game == null) return;
            _game.StateChanged -= OnStateChanged;
            _game.LevelCompleted -= OnLevelCompleted;
            _game.LevelStuck -= OnLevelStuck;
        }

        private void Update()
        {
            if (!TryGetPointerDown(out Vector2 screenPosition)) return;

            Vector2 world = _camera.ScreenToWorldPoint(screenPosition);
            for (int i = _hitAreas.Count - 1; i >= 0; i--)
            {
                if (!_hitAreas[i].Rect.Contains(world)) continue;
                // The handler may rebuild the board and clear the list, so stop right after it.
                _hitAreas[i].OnTap();
                return;
            }
        }

        private static bool TryGetPointerDown(out Vector2 screenPosition)
        {
#if ENABLE_INPUT_SYSTEM
            var pointer = UnityEngine.InputSystem.Pointer.current;
            if (pointer != null && pointer.press.wasPressedThisFrame)
            {
                screenPosition = pointer.position.ReadValue();
                return true;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetMouseButtonDown(0))
            {
                screenPosition = Input.mousePosition;
                return true;
            }
#endif
            screenPosition = default;
            return false;
        }

        private void OnStateChanged()
        {
            _selectedStack = -1;
            _hint = null;
            _overlayText = null;
            _overlayTap = null;
            Rebuild();
        }

        private void OnLevelCompleted(int stars) =>
            ShowOverlay($"Tebrikler!\n{stars} / 3 yıldız\n\nDevam etmek için dokun", _game.NextLevel);

        private void OnLevelStuck() => ShowOverlay("Sıkıştın!\nGeri al ya da yeniden başla", null);

        private void ShowOverlay(string text, Action onTap)
        {
            _overlayText = text;
            _overlayTap = onTap;
            Rebuild();
        }

        private void OnOverlayTapped()
        {
            if (_overlayTap != null)
            {
                _overlayTap();
                return;
            }
            _overlayText = null;
            Rebuild();
        }

        private void OnStackTapped(int stack)
        {
            var state = _game.State;
            if (_selectedStack < 0)
            {
                if (state.TopColor(stack) != BoardState.Empty) _selectedStack = stack;
            }
            else if (_selectedStack == stack)
            {
                _selectedStack = -1;
            }
            else if (_game.TryMove(Move.Shift(_selectedStack, stack)))
            {
                return; // StateChanged already rebuilt the board.
            }
            else
            {
                _selectedStack = state.TopColor(stack) != BoardState.Empty ? stack : -1;
            }
            Rebuild();
        }

        private void OnDockTapped(int dock)
        {
            if (_selectedStack < 0) return;
            if (_game.TryMove(Move.Load(_selectedStack, dock))) return;

            _selectedStack = -1;
            Rebuild();
        }

        private void OnHintTapped()
        {
            _game.RequestHint(move =>
            {
                _hint = move;
                if (move == null) ShowOverlay("İpucu bulunamadı", null);
                else Rebuild();
            });
        }

        private void Rebuild()
        {
            if (_content != null) Destroy(_content.gameObject);
            _content = new GameObject("Board").transform;
            _content.SetParent(transform, false);
            _hitAreas.Clear();

            var state = _game.State;
            if (state == null) return;

            float boardWidth = state.StackCount * ContainerWidth + (state.StackCount - 1) * StackGap;
            float halfWidthNeeded = Mathf.Max(boardWidth, 7f) / 2f + 0.5f;
            _camera.orthographicSize = Mathf.Max(6.2f, halfWidthNeeded / _camera.aspect);

            DrawHeader(state);
            DrawQueue(state);
            DrawDocks(state);
            DrawStacks(state, boardWidth);
            DrawButtons(state);
            if (_overlayText != null) DrawOverlay();
        }

        private void DrawHeader(BoardState state)
        {
            Label(new Vector2(0f, TitleY), $"Seviye {_game.LevelNumber}", 0.6f, TextColor);
            Label(new Vector2(0f, TitleY - 0.65f), $"Hamle {state.MoveCount}   |   Hedef {_game.Level.ParMoves}", 0.32f, DimTextColor);
        }

        private void DrawQueue(BoardState state)
        {
            var ships = new List<Ship>(state.GetWaitingShips());
            if (ships.Count == 0)
            {
                Label(new Vector2(0f, QueueY), "Sırada gemi yok", 0.28f, DimTextColor);
                return;
            }

            Label(new Vector2(0f, QueueY + 0.45f), "Sıradaki gemiler", 0.25f, DimTextColor);
            const float width = 0.6f;
            const float gap = 0.15f;
            float x = -(ships.Count * width + (ships.Count - 1) * gap) / 2f + width / 2f;
            foreach (var ship in ships)
            {
                Box(new Vector2(x, QueueY), new Vector2(width, 0.3f), ColorOf(ship.Color), 1);
                x += width + gap;
            }
        }

        private void DrawDocks(BoardState state)
        {
            float x = -(state.DockCount * DockWidth + (state.DockCount - 1) * DockGap) / 2f + DockWidth / 2f;
            for (int d = 0; d < state.DockCount; d++)
            {
                var center = new Vector2(x, DockY);
                var size = new Vector2(DockWidth, 1.1f);
                if (_hint is Move hint && hint.Type == MoveType.Load && hint.To == d)
                    Box(center, size + Vector2.one * 0.16f, HintColor, -1);
                Box(center, size, PanelDark, 0);

                var ship = state.GetDockedShip(d);
                if (ship == null)
                {
                    Label(center, "Boş rıhtım", 0.28f, DimTextColor);
                }
                else
                {
                    Color shipColor = ColorOf(ship.Color);
                    Box(center, new Vector2(DockWidth - 0.3f, 0.8f), Color.Lerp(shipColor, Color.black, 0.45f), 1);
                    float slotX = center.x - (ship.Capacity * SlotSize + (ship.Capacity - 1) * 0.1f) / 2f + SlotSize / 2f;
                    for (int i = 0; i < ship.Capacity; i++)
                    {
                        var slotColor = i < state.GetDockLoad(d) ? shipColor : PanelDark;
                        Box(new Vector2(slotX, center.y), new Vector2(SlotSize, SlotSize), slotColor, 2);
                        slotX += SlotSize + 0.1f;
                    }
                }

                int dock = d;
                AddHitArea(center, size, () => OnDockTapped(dock));
                x += DockWidth + DockGap;
            }
        }

        private void DrawStacks(BoardState state, float boardWidth)
        {
            float slotHeight = state.StackCapacity * ContainerHeight;
            float x = -boardWidth / 2f + ContainerWidth / 2f;
            for (int s = 0; s < state.StackCount; s++)
            {
                var stack = state.GetStack(s);
                var center = new Vector2(x, StackBaseY + slotHeight / 2f);
                var size = new Vector2(ContainerWidth + 0.15f, slotHeight + 0.15f);
                bool selected = s == _selectedStack;

                if (_hint is Move hint && (hint.From == s || (hint.Type == MoveType.Shift && hint.To == s)))
                    Box(center, size + Vector2.one * 0.16f, HintColor, -1);
                Box(center, size, selected ? Panel : PanelDark, 0);

                for (int i = 0; i < stack.Count; i++)
                {
                    float y = StackBaseY + ContainerHeight * (i + 0.5f);
                    if (selected && i == stack.Count - 1) y += 0.3f;
                    Box(new Vector2(x, y), new Vector2(ContainerWidth - 0.08f, ContainerHeight - 0.06f), ColorOf(stack[i]), 2);
                }

                int index = s;
                AddHitArea(center, new Vector2(ContainerWidth + StackGap, slotHeight + 0.8f), () => OnStackTapped(index));
                x += ContainerWidth + StackGap;
            }
        }

        private void DrawButtons(BoardState state)
        {
            Button(new Vector2(-2.35f, ButtonY), "Geri Al", _game.CanUndo, _game.Undo);
            Button(new Vector2(0f, ButtonY), "Yeniden", true, _game.Restart);
            Button(new Vector2(2.35f, ButtonY), "İpucu", !state.IsWon, OnHintTapped);
        }

        private void DrawOverlay()
        {
            float height = _camera.orthographicSize * 2f;
            var size = new Vector2(height * _camera.aspect, height);
            Box(Vector2.zero, size, new Color(0f, 0f, 0f, 0.75f), 10);
            Label(Vector2.zero, _overlayText, 0.45f, TextColor, 11);
            AddHitArea(Vector2.zero, size, OnOverlayTapped);
        }

        private void Button(Vector2 center, string text, bool enabled, Action onTap)
        {
            var size = new Vector2(2.1f, 0.8f);
            Box(center, size, enabled ? Panel : PanelDark, 0);
            Label(center, text, 0.3f, enabled ? TextColor : DimTextColor);
            if (enabled) AddHitArea(center, size, onTap);
        }

        private void AddHitArea(Vector2 center, Vector2 size, Action onTap) =>
            _hitAreas.Add(new HitArea(new Rect(center - size / 2f, size), onTap));

        private static Color ColorOf(int color) => Palette[color % Palette.Length];

        private void Box(Vector2 center, Vector2 size, Color color, int order)
        {
            var go = new GameObject("Box");
            go.transform.SetParent(_content, false);
            go.transform.localPosition = center;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            var sprite = go.AddComponent<SpriteRenderer>();
            sprite.sprite = _square;
            sprite.color = color;
            sprite.sortingOrder = order;
        }

        private void Label(Vector2 position, string text, float height, Color color, int order = 5)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(_content, false);
            go.transform.localPosition = position;
            var meshRenderer = go.AddComponent<MeshRenderer>();
            var mesh = go.AddComponent<TextMesh>();
            mesh.font = _font;
            mesh.fontSize = 64;
            // A TextMesh line is roughly fontSize * characterSize / 10 world units tall.
            mesh.characterSize = height * 10f / mesh.fontSize;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.color = color;
            mesh.text = text;
            meshRenderer.sharedMaterial = _font.material;
            meshRenderer.sortingOrder = order;
        }
    }
}
