using UnityEngine;

namespace PixelDungeon
{
    /// <summary>
    /// Ambush chest (design doc 1.4 / 3.5): looks like loot, but bites. Wakes when the player opens it
    /// or wanders too close, replacing itself with a hostile Mimic monster.
    /// </summary>
    public class MimicChest : Interactable
    {
        private bool _triggered;
        public Room Room;

        public override bool CanInteract => !_triggered;
        public override string Prompt => "Open chest";

        public static MimicChest Spawn(Vector2 pos, Room room)
        {
            var go = new GameObject("MimicChest") { transform = { position = pos } };
            var m = go.AddComponent<MimicChest>();
            m.Room = room;
            m.AddSprite("ChestGolden", 14);
            m.AddTrigger(0.7f);
            return m;
        }

        private void Update()
        {
            if (_triggered) return;
            var player = GameManager.I != null ? GameManager.I.Player : null;
            if (player != null && Vector2.Distance(transform.position, player.transform.position) < 1.1f)
                Wake();
        }

        public override void Interact(PlayerController pc) => Wake();

        private void Wake()
        {
            if (_triggered) return;
            _triggered = true;
            CombatService.I.Burst(transform.position, new Color(1f, 0.8f, 0.3f), 14);
            Juice.Shake(0.2f);

            if (Room != null) Room.SpawnEnemy("mimic", transform.position);
            else GameManager.I.SpawnLooseEnemy("mimic", transform.position);

            Destroy(gameObject);
        }
    }
}
