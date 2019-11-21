﻿// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osuTK;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Game.Beatmaps;

namespace osu.Game.Storyboards.Drawables
{
    public class DrawableStoryboardSprite : Sprite, IFlippable
    {
        public StoryboardSprite Sprite { get; private set; }

        public bool FlipH { get; set; }
        public bool FlipV { get; set; }

        public override bool RemoveWhenNotAlive => false;

        protected override Vector2 DrawScale
            => new Vector2(FlipH ? -base.DrawScale.X : base.DrawScale.X, FlipV ? -base.DrawScale.Y : base.DrawScale.Y);

        public override Anchor Origin
        {
            get
            {
                var origin = base.Origin;

                if (FlipH)
                {
                    if (origin.HasFlag(Anchor.x0))
                        origin = Anchor.x2 | (origin & (Anchor.y0 | Anchor.y1 | Anchor.y2));
                    else if (origin.HasFlag(Anchor.x2))
                        origin = Anchor.x0 | (origin & (Anchor.y0 | Anchor.y1 | Anchor.y2));
                }

                if (FlipV)
                {
                    if (origin.HasFlag(Anchor.y0))
                        origin = Anchor.y2 | (origin & (Anchor.x0 | Anchor.x1 | Anchor.x2));
                    else if (origin.HasFlag(Anchor.y2))
                        origin = Anchor.y0 | (origin & (Anchor.x0 | Anchor.x1 | Anchor.x2));
                }

                return origin;
            }
        }

        public override bool IsPresent
            => !float.IsNaN(DrawPosition.X) && !float.IsNaN(DrawPosition.Y) && base.IsPresent;

        public DrawableStoryboardSprite(StoryboardSprite sprite)
        {
            Sprite = sprite;
            Origin = sprite.Origin;
            Position = sprite.InitialPosition;

            LifetimeStart = sprite.StartTime;
            LifetimeEnd = sprite.EndTime;
        }

        [BackgroundDependencyLoader]
        private void load(IBindable<WorkingBeatmap> beatmap, TextureStore textureStore)
        {
            var path = beatmap.Value.BeatmapSetInfo?.Files?.Find(f => f.Filename.Equals(Sprite.Path, StringComparison.InvariantCultureIgnoreCase))?.FileInfo.StoragePath;
            if (path == null)
                return;

            Texture = textureStore.Get(path);
            Sprite.ApplyTransforms(this);
        }
    }
}
