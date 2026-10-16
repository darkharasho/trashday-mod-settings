namespace ModSettings
{
    /// <summary>When to save changed config files: once the first unsaved change is a second old.</summary>
    internal sealed class SaveSchedule
    {
        private readonly float _interval;
        private float _due;

        public SaveSchedule(float interval = 1f)
        {
            _interval = interval;
        }

        public bool Dirty { get; private set; }

        public void MarkDirty(float now)
        {
            if (Dirty)
                return;
            Dirty = true;
            _due = now + _interval;
        }

        public bool ShouldSave(float now) => Dirty && now >= _due;

        public void Saved() => Dirty = false;
    }
}
