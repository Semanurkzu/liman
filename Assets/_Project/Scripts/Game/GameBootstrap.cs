using UnityEngine;

namespace Liman.Game
{
    /// <summary>
    /// Builds the game from code after any scene loads, so the project runs with an empty scene
    /// and no prefabs. Replace with a hand-made scene once art and UI are in place.
    /// </summary>
    public static class GameBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (Object.FindAnyObjectByType<GameController>() != null) return;

            var root = new GameObject("Liman");
            var controller = root.AddComponent<GameController>();
            var view = root.AddComponent<BoardView>();
            view.Bind(controller);
            controller.Initialize(new PlaceholderAdService());
        }
    }
}
