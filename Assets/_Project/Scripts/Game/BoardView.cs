using System;
using System.Collections;
using System.Collections.Generic;
using Liman.Core;
using UnityEngine;

namespace Liman.Game
{
    /// <summary>
    /// Placeholder renderer and input handler made of coloured boxes and TextMesh labels.
    /// It rebuilds the whole board on every change, which is fine at this scale; moves are animated by
    /// drawing the state before the move and tweening the moved container (and any departing ship) on top.
    /// Replace with pooled sprites when real art arrives.
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
        private const float QueueY = 3.7f;
        private const float TitleY = 5.4f;
        private const float ButtonY = -4.7f;
        private const float SlotSize = 0.45f;
        private const float SlotGap = 0.1f;
        private const float QueueShipWidth = 0.6f;
        private const float QueueShipGap = 0.15f;
        private const float SelectedLift = 0.3f;
        private const float HeavyFrame = 0.08f;

        private static readonly Vector2 ContainerSize = new Vector2(ContainerWidth - 0.08f, ContainerHeight - 0.06f);

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
        private static readonly Color CraneColor = new Color32(244, 162, 38, 255);
        private static readonly Color CableColor = new Color32(120, 132, 145, 255);
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
        private readonly List<Transform> _stackTops = new List<Transform>();
        private readonly List<Transform> _dockGroups = new List<Transform>();
        private readonly List<Transform> _shipGroups = new List<Transform>();
        private GameController _game;
        private Camera _camera;
        private SoundEffects _sfx;
        private Sprite _square;
        private Font _font;
        private Transform _content;
        private int _selectedStack = -1;
        private Move? _hint;
        private string _overlayText;
        private Action _overlayTap;
        private float _overlayTextHeight = 0.45f;
        private Coroutine _animation;
        private bool _animating;
        private Action _afterAnimation;

        public void Bind(GameController game)
        {
            _game = game;
            _game.StateChanged += OnStateChanged;
            _game.MoveApplied += OnMoveApplied;
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
            if (FindAnyObjectByType<AudioListener>() == null) _camera.gameObject.AddComponent<AudioListener>();

            _sfx = gameObject.AddComponent<SoundEffects>();

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
            _game.MoveApplied -= OnMoveApplied;
            _game.LevelCompleted -= OnLevelCompleted;
            _game.LevelStuck -= OnLevelStuck;
        }

        private void Update()
        {
            if (!TryGetPointerDown(out Vector2 screenPosition)) return;

            // A tap during an animation snaps it to the end so fast players never wait. If that reveals
            // an overlay, the tap is swallowed so it doesn't dismiss the overlay unseen.
            if (_animating)
            {
                FinishAnimation();
                if (_overlayText != null) return;
            }

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
            if (_animating) return;
            Rebuild();
            if (_game.State != null && _game.State.MoveCount == 0) ShowMechanicIntro();
        }

        /// <summary>Explains a special container type once, on the first level the player meets it.</summary>
        private void ShowMechanicIntro()
        {
            if (TryIntro(GeneratorSettings.FirstHeavyLevel, "liman.intro.heavy",
                    "Yeni: Ağır konteyner\n\nKalın çerçeveli konteynerler başka bir\nkonteynerin üstüne konamaz. Sadece\nboş yığına ya da gemiye gidebilir.\n\nDevam etmek için dokun"))
                return;
            TryIntro(GeneratorSettings.FirstLockedLevel, "liman.intro.locked",
                "Yeni: Kilitli konteyner\n\nKilitli konteyner, kendi rengindeki\ngemi rıhtıma yanaşana kadar\nyerinden oynamaz.\n\nDevam etmek için dokun");
        }

        private bool TryIntro(int firstLevel, string seenKey, string text)
        {
            if (_game.LevelNumber < firstLevel || PlayerPrefs.GetInt(seenKey, 0) == 1) return false;
            PlayerPrefs.SetInt(seenKey, 1);
            PlayerPrefs.Save();
            ShowOverlay(text, null, 0.3f);
            return true;
        }

        private void OnMoveApplied(BoardState before, Move move)
        {
            if (_animating) FinishAnimation();
            _selectedStack = -1;
            _hint = null;
            _animating = true;
            _animation = StartCoroutine(AnimateMove(before, move));
        }

        private void OnLevelCompleted(int stars) => AfterAnimation(() =>
        {
            _sfx.Play(_sfx.Win);
            ShowOverlay($"Tebrikler!\n{stars} / 3 yıldız\n\nDevam etmek için dokun", _game.NextLevel);
        });

        private void OnLevelStuck() => AfterAnimation(() =>
        {
            _sfx.Play(_sfx.Error);
            ShowOverlay("Sıkıştın!\nGeri al ya da yeniden başla", null);
        });

        private void AfterAnimation(Action action)
        {
            if (_animating) _afterAnimation += action;
            else action();
        }

        private void FinishAnimation()
        {
            if (_animation != null) StopCoroutine(_animation);
            _animation = null;
            _animating = false;
            Rebuild();

            var pending = _afterAnimation;
            _afterAnimation = null;
            pending?.Invoke();
        }

        private void ShowOverlay(string text, Action onTap, float textHeight = 0.45f)
        {
            _overlayText = text;
            _overlayTap = onTap;
            _overlayTextHeight = textHeight;
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
            if (_selectedStack == stack)
            {
                _selectedStack = -1;
                Rebuild();
                return;
            }
            if (_selectedStack >= 0)
            {
                if (_game.TryMove(Move.Shift(_selectedStack, stack))) return; // MoveApplied took over drawing.

                // A heavy container onto its own colour would be legal for a plain one: explain by refusing,
                // rather than silently switching the selection to the target stack.
                int selected = state.TopContainer(_selectedStack);
                if (Container.IsHeavy(selected) && state.TopColor(stack) == Container.ColorOf(selected))
                {
                    Refuse(_stackTops[_selectedStack]);
                    return;
                }
            }

            if (state.IsTopMovable(stack))
            {
                Select(stack);
            }
            else if (state.TopContainer(stack) != BoardState.Empty)
            {
                Refuse(_stackTops[stack]); // Locked until its ship docks.
            }
            else if (_selectedStack >= 0)
            {
                _selectedStack = -1;
                Rebuild();
            }
        }

        private void Refuse(Transform target)
        {
            _sfx.Play(_sfx.Error, 0.8f);
            StartCoroutine(Shake(target));
        }

        private void Select(int stack)
        {
            _selectedStack = stack;
            Rebuild();
            _sfx.Play(_sfx.Pick, 0.7f);
            var top = _stackTops[stack];
            if (top == null) return;
            Vector3 raised = top.localPosition;
            Vector3 resting = raised - new Vector3(0f, SelectedLift, 0f);
            StartCoroutine(Tween(0.09f, t =>
            {
                if (top != null) top.localPosition = Vector3.LerpUnclamped(resting, raised, EaseOutBack(t));
            }));
        }

        private void OnDockTapped(int dock)
        {
            if (_selectedStack < 0) return;
            if (_game.TryMove(Move.Load(_selectedStack, dock))) return;

            // Wrong colour: keep the selection so the player can pick the right ship, and shake this one.
            Refuse(_shipGroups[dock] != null ? _shipGroups[dock] : _dockGroups[dock]);
        }

        private void OnHintTapped()
        {
            _game.RequestHint((outcome, move) =>
            {
                switch (outcome)
                {
                    case HintOutcome.Found:
                        _hint = move;
                        Rebuild();
                        break;
                    case HintOutcome.Unsolvable:
                        ShowOverlay("Bu durumdan çıkış yok!\nGeri al ya da yeniden başla", null);
                        break;
                    case HintOutcome.NotFound:
                        ShowOverlay("İpucu bulunamadı", null);
                        break;
                }
            });
        }

        private void OnSoundTapped()
        {
            _sfx.Muted = !_sfx.Muted;
            Rebuild();
            _sfx.Play(_sfx.Pick, 0.7f);
        }

        private IEnumerator AnimateMove(BoardState before, Move move)
        {
            Render(before, move.From);

            int container = before.TopContainer(move.From);
            int fromIndex = before.GetStack(move.From).Count - 1;
            Vector2 start = ContainerPosition(before, move.From, fromIndex) + new Vector2(0f, SelectedLift);
            var containerSize = ContainerSize;

            Vector2 end;
            Vector2 endSize;
            bool departs = false;
            if (move.Type == MoveType.Shift)
            {
                end = ContainerPosition(before, move.To, before.GetStack(move.To).Count);
                endSize = containerSize;
            }
            else
            {
                var ship = before.GetDockedShip(move.To);
                int load = before.GetDockLoad(move.To);
                end = SlotPosition(DockCenter(before, move.To), ship.Capacity, load);
                endSize = new Vector2(SlotSize, SlotSize);
                departs = load + 1 == ship.Capacity;
            }

            var box = DrawContainer(start, containerSize, container, true, 6);
            var spreader = Box(start, new Vector2(ContainerWidth, 0.08f), CraneColor, 8);
            var cable = Box(start, new Vector2(0.05f, 1f), CableColor, -2);
            float top = _camera.orthographicSize + 1f;
            void Place(Vector2 position, Vector2 size)
            {
                box.localPosition = position;
                box.localScale = new Vector3(size.x, size.y, 1f);
                float spreaderY = position.y + size.y / 2f + 0.04f;
                spreader.localPosition = new Vector2(position.x, spreaderY);
                cable.localPosition = new Vector2(position.x, (spreaderY + top) / 2f);
                cable.localScale = new Vector3(0.05f, top - spreaderY, 1f);
            }

            float carryY = Mathf.Min(StackBaseY + before.StackCapacity * ContainerHeight + 0.6f, DockY - 1f);
            var lifted = new Vector2(start.x, Mathf.Max(start.y, carryY));
            var over = new Vector2(end.x, lifted.y);
            float travel = Mathf.Clamp(0.08f + 0.035f * Mathf.Abs(end.x - start.x), 0.1f, 0.3f);

            Place(start, containerSize);
            yield return Tween(0.1f, t => Place(Vector2.Lerp(start, lifted, EaseOutCubic(t)), containerSize));
            yield return Tween(travel, t => Place(Vector2.Lerp(lifted, over, EaseInOutQuad(t)), containerSize));
            yield return Tween(0.12f, t =>
                Place(Vector2.Lerp(over, end, EaseInOutQuad(t)), Vector2.Lerp(containerSize, endSize, t)));
            Place(end, endSize);
            _sfx.Play(move.Type == MoveType.Shift ? _sfx.Drop : _sfx.Load);

            Destroy(spreader.gameObject);
            Destroy(cable.gameObject);

            if (departs) yield return SailAway(before, move.To, box);

            _animation = null;
            FinishAnimation();
        }

        private IEnumerator SailAway(BoardState before, int dock, Transform loadedContainer)
        {
            var ship = _shipGroups[dock];
            loadedContainer.SetParent(ship, true);
            yield return new WaitForSeconds(0.12f);

            _sfx.Play(_sfx.Horn);
            SetSortingOffset(ship, 7);
            Vector3 from = ship.localPosition;
            var to = new Vector3(_camera.orthographicSize * _camera.aspect + DockWidth, from.y, 0f);
            yield return Tween(0.5f, t =>
            {
                ship.localPosition = Vector3.LerpUnclamped(from, to, EaseInCubic(t));
                ship.localScale = Vector3.one * Mathf.Lerp(1f, 0.85f, t);
            });

            var after = _game.State;
            Render(after, -1);
            var arriving = _shipGroups[dock];
            if (arriving == null) yield break;

            // The next ship comes from the front of the queue as it was before the move.
            int waiting = before.ShipsWaiting;
            float queueStartX = -(waiting * QueueShipWidth + (waiting - 1) * QueueShipGap) / 2f + QueueShipWidth / 2f;
            var queuePosition = new Vector3(queueStartX, QueueY, 0f);
            Vector3 berth = arriving.localPosition;
            yield return Tween(0.35f, t =>
            {
                float e = EaseOutCubic(t);
                arriving.localPosition = Vector3.LerpUnclamped(queuePosition, berth, e);
                arriving.localScale = Vector3.one * Mathf.Lerp(0.3f, 1f, e);
            });
        }

        private void Rebuild() => Render(_game.State, -1);

        /// <summary>Draws the board for <paramref name="state"/>, optionally leaving out the top container of one stack.</summary>
        private void Render(BoardState state, int hiddenStackTop)
        {
            if (_content != null) Destroy(_content.gameObject);
            _content = new GameObject("Board").transform;
            _content.SetParent(transform, false);
            _hitAreas.Clear();
            _stackTops.Clear();
            _dockGroups.Clear();
            _shipGroups.Clear();

            if (state == null)
            {
                DrawLoading();
                return;
            }

            float halfWidthNeeded = Mathf.Max(BoardWidth(state), 7f) / 2f + 0.5f;
            _camera.orthographicSize = Mathf.Max(6.2f, halfWidthNeeded / _camera.aspect);

            DrawHeader();
            DrawQueue(state);
            DrawDocks(state);
            DrawStacks(state, hiddenStackTop);
            DrawButtons(state);
            if (_overlayText != null && !_animating) DrawOverlay();
        }

        private void DrawLoading()
        {
            if (!_game.IsLoading)
            {
                Label(Vector2.zero, "Seviye yüklenemedi\n\nTekrar denemek için dokun", 0.35f, TextColor);
                AddHitArea(Vector2.zero, new Vector2(100f, 100f), () => _game.StartLevel(_game.LevelNumber));
                return;
            }

            // A container swinging from a crane cable. Hidden at first so quick loads don't flash.
            var loading = Group("Loading", Vector2.zero);
            loading.gameObject.SetActive(false);
            var pivot = Group("Pivot", new Vector2(0f, 1.2f));
            pivot.SetParent(loading, false);
            Box(new Vector2(0f, -0.6f), new Vector2(0.05f, 1.2f), CableColor, 1, pivot);
            Box(new Vector2(0f, -1.24f), new Vector2(ContainerWidth, 0.08f), CraneColor, 3, pivot);
            DrawContainer(new Vector2(0f, -1.52f), ContainerSize, Container.Make(_game.LevelNumber), true, 2, pivot);
            Label(new Vector2(0f, -1.3f), $"Seviye {_game.LevelNumber} hazırlanıyor", 0.3f, DimTextColor, 5, loading);
            StartCoroutine(SwingWhileLoading(loading, pivot));
        }

        private static IEnumerator SwingWhileLoading(Transform loading, Transform pivot)
        {
            yield return new WaitForSecondsRealtime(0.2f);
            if (loading == null) yield break;
            loading.gameObject.SetActive(true);
            for (float t = 0f; pivot != null; t += Time.unscaledDeltaTime)
            {
                pivot.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 3f) * 12f);
                yield return null;
            }
        }

        private void DrawHeader()
        {
            Label(new Vector2(0f, TitleY), $"Seviye {_game.LevelNumber}", 0.6f, TextColor);
            Label(new Vector2(0f, TitleY - 0.65f), $"Hamle {_game.MoveCount}   |   Hedef {_game.Level.ParMoves}", 0.32f, DimTextColor);

            var soundPosition = new Vector2(_camera.orthographicSize * _camera.aspect - 0.9f, TitleY);
            Label(soundPosition, _sfx.Muted ? "Ses: Kapalı" : "Ses: Açık", 0.22f, DimTextColor);
            AddHitArea(soundPosition, new Vector2(1.6f, 0.6f), OnSoundTapped);
        }

        private void DrawQueue(BoardState state)
        {
            var ships = new List<Ship>(state.GetWaitingShips());
            if (ships.Count == 0)
            {
                Label(new Vector2(0f, QueueY), "Sırada gemi yok", 0.28f, DimTextColor);
                return;
            }

            Label(new Vector2(0f, QueueY + 0.4f), "Sıradaki gemiler", 0.25f, DimTextColor);
            float x = -(ships.Count * QueueShipWidth + (ships.Count - 1) * QueueShipGap) / 2f + QueueShipWidth / 2f;
            foreach (var ship in ships)
            {
                Box(new Vector2(x, QueueY), new Vector2(QueueShipWidth, 0.3f), ColorOf(ship.Color), 1);
                x += QueueShipWidth + QueueShipGap;
            }
        }

        private void DrawDocks(BoardState state)
        {
            for (int d = 0; d < state.DockCount; d++)
            {
                var center = DockCenter(state, d);
                var size = new Vector2(DockWidth, 1.1f);
                if (_hint is Move hint && hint.Type == MoveType.Load && hint.To == d)
                    Box(center, size + Vector2.one * 0.16f, HintColor, -1);

                var dockGroup = Group($"Dock {d}", center);
                _dockGroups.Add(dockGroup);
                Box(Vector2.zero, size, PanelDark, 0, dockGroup);

                var ship = state.GetDockedShip(d);
                if (ship == null)
                {
                    Label(Vector2.zero, "Boş rıhtım", 0.28f, DimTextColor, 5, dockGroup);
                    _shipGroups.Add(null);
                }
                else
                {
                    var shipGroup = Group($"Ship {d}", center);
                    _shipGroups.Add(shipGroup);
                    Color shipColor = ColorOf(ship.Color);
                    Box(Vector2.zero, new Vector2(DockWidth - 0.3f, 0.8f), Color.Lerp(shipColor, Color.black, 0.45f), 1, shipGroup);
                    for (int i = 0; i < ship.Capacity; i++)
                    {
                        var slotColor = i < state.GetDockLoad(d) ? shipColor : PanelDark;
                        Box(SlotPosition(Vector2.zero, ship.Capacity, i), new Vector2(SlotSize, SlotSize), slotColor, 2, shipGroup);
                    }
                }

                int dock = d;
                AddHitArea(center, size, () => OnDockTapped(dock));
            }
        }

        private void DrawStacks(BoardState state, int hiddenStackTop)
        {
            float slotHeight = state.StackCapacity * ContainerHeight;
            for (int s = 0; s < state.StackCount; s++)
            {
                var stack = state.GetStack(s);
                float x = StackX(state, s);
                var center = new Vector2(x, StackBaseY + slotHeight / 2f);
                var size = new Vector2(ContainerWidth + 0.15f, slotHeight + 0.15f);
                bool selected = s == _selectedStack;

                if (_hint is Move hint && (hint.From == s || (hint.Type == MoveType.Shift && hint.To == s)))
                    Box(center, size + Vector2.one * 0.16f, HintColor, -1);
                Box(center, size, selected ? Panel : PanelDark, 0);

                int visible = s == hiddenStackTop ? stack.Count - 1 : stack.Count;
                Transform top = null;
                for (int i = 0; i < visible; i++)
                {
                    var position = ContainerPosition(state, s, i);
                    if (selected && i == stack.Count - 1) position.y += SelectedLift;
                    top = DrawContainer(position, ContainerSize, stack[i], state.IsMovable(stack[i]), 2);
                }
                _stackTops.Add(top);

                int index = s;
                AddHitArea(center, new Vector2(ContainerWidth + StackGap, slotHeight + 0.8f), () => OnStackTapped(index));
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
            var dim = Box(Vector2.zero, size, new Color(0f, 0f, 0f, 0.75f), 10);
            var label = Label(Vector2.zero, _overlayText, _overlayTextHeight, TextColor, 11);
            AddHitArea(Vector2.zero, size, OnOverlayTapped);

            var dimSprite = dim.GetComponent<SpriteRenderer>();
            StartCoroutine(Tween(0.2f, t =>
            {
                if (dim == null || label == null) return;
                dimSprite.color = new Color(0f, 0f, 0f, 0.75f * t);
                label.localScale = Vector3.one * Mathf.LerpUnclamped(0.6f, 1f, EaseOutBack(t));
            }));
        }

        private void Button(Vector2 center, string text, bool enabled, Action onTap)
        {
            var size = new Vector2(2.1f, 0.8f);
            Box(center, size, enabled ? Panel : PanelDark, 0);
            Label(center, text, 0.3f, enabled ? TextColor : DimTextColor);
            if (enabled) AddHitArea(center, size, onTap);
        }

        private static float BoardWidth(BoardState state) =>
            state.StackCount * ContainerWidth + (state.StackCount - 1) * StackGap;

        private static float StackX(BoardState state, int stack) =>
            -BoardWidth(state) / 2f + ContainerWidth / 2f + stack * (ContainerWidth + StackGap);

        private static Vector2 ContainerPosition(BoardState state, int stack, int index) =>
            new Vector2(StackX(state, stack), StackBaseY + ContainerHeight * (index + 0.5f));

        private static Vector2 DockCenter(BoardState state, int dock)
        {
            float firstX = -(state.DockCount * DockWidth + (state.DockCount - 1) * DockGap) / 2f + DockWidth / 2f;
            return new Vector2(firstX + dock * (DockWidth + DockGap), DockY);
        }

        private static Vector2 SlotPosition(Vector2 dockCenter, int capacity, int slot)
        {
            float firstX = dockCenter.x - (capacity * SlotSize + (capacity - 1) * SlotGap) / 2f + SlotSize / 2f;
            return new Vector2(firstX + slot * (SlotSize + SlotGap), dockCenter.y);
        }

        private void AddHitArea(Vector2 center, Vector2 size, Action onTap) =>
            _hitAreas.Add(new HitArea(new Rect(center - size / 2f, size), onTap));

        private static Color ColorOf(int color) => Palette[color % Palette.Length];

        private Transform Group(string name, Vector2 position)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_content, false);
            go.transform.localPosition = position;
            return go.transform;
        }

        /// <summary>
        /// Draws a container as a group scaled to <paramref name="size"/>, so callers can move and resize it as one
        /// box. Children are laid out in unit space with sizes converted from world units at the given size.
        /// </summary>
        private Transform DrawContainer(Vector2 center, Vector2 size, int container, bool movable, int order, Transform parent = null)
        {
            var group = new GameObject("Container").transform;
            group.SetParent(parent != null ? parent : _content, false);
            group.localPosition = center;
            group.localScale = new Vector3(size.x, size.y, 1f);
            Vector2 Unit(float worldX, float worldY) => new Vector2(worldX / size.x, worldY / size.y);

            Color color = ColorOf(Container.ColorOf(container));
            if (!movable) color = Color.Lerp(color, Color.black, 0.35f);

            if (Container.IsHeavy(container))
            {
                // A thick dark frame: reads as reinforced steel at a glance.
                Box(Vector2.zero, Vector2.one, Color.Lerp(color, Color.black, 0.6f), order, group);
                Box(Vector2.zero, Vector2.one - Unit(2f * HeavyFrame, 2f * HeavyFrame), color, order + 1, group);
            }
            else
            {
                Box(Vector2.zero, Vector2.one, color, order, group);
            }

            if (Container.IsLocked(container))
            {
                // Padlock: closed and solid while locked, open and faded once its ship has docked.
                var lockColor = movable ? new Color(0.1f, 0.1f, 0.1f, 0.35f) : new Color(0.1f, 0.1f, 0.1f, 0.9f);
                float shackleLift = movable ? 0.05f : 0f;
                Box(Unit(0f, -0.04f), Unit(0.22f, 0.15f), lockColor, order + 2, group);
                Box(Unit(-0.08f, 0.07f + shackleLift), Unit(0.04f, 0.1f), lockColor, order + 2, group);
                Box(Unit(0.08f, 0.07f), Unit(0.04f, 0.1f), lockColor, order + 2, group);
                Box(Unit(0f, 0.12f + shackleLift), Unit(0.2f, 0.04f), lockColor, order + 2, group);
            }
            return group;
        }

        private Transform Box(Vector2 center, Vector2 size, Color color, int order, Transform parent = null)
        {
            var go = new GameObject("Box");
            go.transform.SetParent(parent != null ? parent : _content, false);
            go.transform.localPosition = center;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            var sprite = go.AddComponent<SpriteRenderer>();
            sprite.sprite = _square;
            sprite.color = color;
            sprite.sortingOrder = order;
            return go.transform;
        }

        private Transform Label(Vector2 position, string text, float height, Color color, int order = 5, Transform parent = null)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent != null ? parent : _content, false);
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
            return go.transform;
        }

        private static void SetSortingOffset(Transform root, int offset)
        {
            foreach (var r in root.GetComponentsInChildren<Renderer>())
                r.sortingOrder += offset;
        }

        private static IEnumerator Shake(Transform target)
        {
            if (target == null) yield break;
            Vector3 origin = target.localPosition;
            yield return Tween(0.25f, t =>
            {
                if (target != null)
                    target.localPosition = origin + new Vector3(Mathf.Sin(t * Mathf.PI * 6f) * 0.12f * (1f - t), 0f, 0f);
            });
        }

        /// <summary>Calls <paramref name="step"/> with progress 0..1 each frame, ending exactly at 1.</summary>
        private static IEnumerator Tween(float duration, Action<float> step)
        {
            for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
            {
                step(elapsed / duration);
                yield return null;
            }
            step(1f);
        }

        private static float EaseOutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);

        private static float EaseInCubic(float t) => t * t * t;

        private static float EaseInOutQuad(float t) => t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;

        private static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }
    }
}
