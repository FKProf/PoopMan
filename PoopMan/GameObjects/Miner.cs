using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using PoopManLibrary;
using PoopManLibrary.Graphics;

namespace PoopMan.GameObjects;

public class Miner
{
    private static readonly TimeSpan p_movementTime = TimeSpan.FromMilliseconds(250);

    private TimeSpan _movementTimer;

    private Vector2 _nextDirection;

    private float _stride;

    
}