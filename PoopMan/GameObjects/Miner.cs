using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using PoopManLibrary;
using PoopManLibrary.World;

namespace PoopMan.GameObjects;

public class Miner
{
    // === Posizione (identica a Player) ===
    public Point TilePosition;
    public Vector2 Position;
    private Vector2 targetPosition;
    private float moveSpeed = 120f;
    private bool isMoving = false;

    // === Animazione (identica a Player) ===
    private Texture2D texture;
    private float animationTimer = 0f;
    private float animationSpeed = 0.15f;
    private int currentFrame = 0;
    private Dictionary<string, List<Rectangle>> animations = new();
    private string currentAnimation = "idle_front";
    private List<Rectangle> currentAnimationFrames;

    private enum MinerState
    {
        IdleFront, IdleBack, IdleLeft, IdleRight,
        WalkFront, WalkBack, WalkLeft, WalkRight
    }
    private MinerState state = MinerState.IdleFront;

    // === Specifico Miner: corpo a serpente ===
    private List<(Vector2 from, Vector2 to)> _bodySegments = new();
    private float _movementProgress = 0f;
    private Vector2 _currentDirection = Vector2.UnitX;

    private const int MAX_BUFFER_SIZE = 2;
    private Queue<Vector2> _inputBuffer = new(MAX_BUFFER_SIZE);

    public event EventHandler BodyCollision;

    public Miner(Point startTile, string xmlPath, ContentManager content)
    {
        LoadAnimationsFromXml(xmlPath, content);

        TilePosition = startTile;
        Position = new Vector2(TilePosition.X * TileMap.TileSize,
                               TilePosition.Y * TileMap.TileSize);
        targetPosition = Position;
    }

    private void LoadAnimationsFromXml(string xmlPath, ContentManager content)
    {
        XDocument doc = XDocument.Load(xmlPath);
        string texturePath = doc.Root.Element("Texture").Value;
        texture = content.Load<Texture2D>(texturePath);

        var temp = new Dictionary<string, List<(int frame, Rectangle rect)>>();

        foreach (var region in doc.Root.Element("Regions").Elements("Region"))
        {
            string fullName = region.Attribute("Name").Value;
            int x = int.Parse(region.Attribute("X").Value);
            int y = int.Parse(region.Attribute("Y").Value);
            int w = int.Parse(region.Attribute("Width").Value);
            int h = int.Parse(region.Attribute("Height").Value);

            int frameNumberStart = fullName.Length;
            while (frameNumberStart > 0 && char.IsDigit(fullName[frameNumberStart - 1]))
                frameNumberStart--;

            if (frameNumberStart >= fullName.Length || frameNumberStart == 0)
                continue;

            string animationName = fullName.Substring(0, frameNumberStart).TrimEnd('_', '-', ' ');
            int frameNumber = int.Parse(fullName.Substring(frameNumberStart));

            if (!temp.ContainsKey(animationName))
                temp[animationName] = new List<(int, Rectangle)>();

            temp[animationName].Add((frameNumber, new Rectangle(x, y, w, h)));
        }

        animations = new Dictionary<string, List<Rectangle>>();
        foreach (var pair in temp)
        {
            animations[pair.Key] = pair.Value
                .OrderBy(f => f.frame)
                .Select(f => f.rect)
                .ToList();
        }
    }

    private void HandleInput()
    {
        var potentialNextDirection = Vector2.Zero;

        if (GameController.HoldUp()) potentialNextDirection = -Vector2.UnitY;
        if (GameController.HoldDown())  potentialNextDirection =  Vector2.UnitY;
        if (GameController.HoldLeft())  potentialNextDirection = -Vector2.UnitX;
        if (GameController.HoldRight()) potentialNextDirection =  Vector2.UnitX;

        if (potentialNextDirection == Vector2.Zero)
        {
            _inputBuffer.Clear();
            return;
        }

        var last = _inputBuffer.Count > 0 ? _inputBuffer.Last() : Vector2.Zero;
        if (last == potentialNextDirection) return;

        if (_inputBuffer.Count < MAX_BUFFER_SIZE)
            _inputBuffer.Enqueue(potentialNextDirection);
    }

    public void Grow()
    {
        if (_bodySegments.Count > 0)
        {
            var tail = _bodySegments[_bodySegments.Count - 1];
            _bodySegments.Add((tail.to, tail.to));
        }
        else
        {
            _bodySegments.Add((targetPosition, targetPosition));
        }
    }

    public void Update(TileMap map, GameTime gameTime)
    {
        HandleInput();

        if (!isMoving)
        {
            if (_inputBuffer.Count > 0)
            {
                _currentDirection = _inputBuffer.Dequeue();

                Point nextTile = new Point(
                    TilePosition.X + (int)_currentDirection.X,
                    TilePosition.Y + (int)_currentDirection.Y
                );

                if (map.IsWalkable(nextTile))
                {
                    for (int i = _bodySegments.Count - 1; i > 0; i--)
                        _bodySegments[i] = (_bodySegments[i].to, _bodySegments[i - 1].to);

                    if (_bodySegments.Count > 0)
                        _bodySegments[0] = (_bodySegments[0].to, targetPosition);

                    Vector2 newHeadTarget = new Vector2(nextTile.X * TileMap.TileSize,
                                                        nextTile.Y * TileMap.TileSize);

                    int segmentRadius = TileMap.TileSize / 2;
                    var headBounds = new Collision(
                        (int)newHeadTarget.X + segmentRadius,
                        (int)newHeadTarget.Y + segmentRadius,
                        segmentRadius
                    );

                    foreach (var (_, to) in _bodySegments.Skip(1))
                    {
                        var segBounds = new Collision(
                            (int)to.X + segmentRadius,
                            (int)to.Y + segmentRadius,
                            segmentRadius
                        );

                        if (headBounds.Intersects(segBounds))
                        {
                            BodyCollision?.Invoke(this, EventArgs.Empty);
                            break;
                        }
                    }

                    TilePosition = nextTile;
                    targetPosition = newHeadTarget;
                    isMoving = true;
                    currentFrame = 0;
                    animationTimer = 0f;
                    _movementProgress = 0f;

                    state = _currentDirection switch
                    {
                        var d when d == Vector2.UnitX => MinerState.WalkRight,
                        var d when d == -Vector2.UnitX => MinerState.WalkLeft,
                        var d when d == -Vector2.UnitY => MinerState.WalkBack,
                        _ => MinerState.WalkFront
                    };
                }
                else
                {
                    state = state switch
                    {
                        MinerState.WalkFront => MinerState.IdleFront,
                        MinerState.WalkBack => MinerState.IdleBack,
                        MinerState.WalkLeft => MinerState.IdleLeft,
                        MinerState.WalkRight => MinerState.IdleRight,
                        _ => state
                    };
                }
            }
            else
            {
                state = state switch
                {
                    MinerState.WalkFront => MinerState.IdleFront,
                    MinerState.WalkBack => MinerState.IdleBack,
                    MinerState.WalkLeft => MinerState.IdleLeft,
                    MinerState.WalkRight => MinerState.IdleRight,
                    _ => state
                };
            }
        }

        if (isMoving)
        {
            Vector2 direction = targetPosition - Position;
            float distance = direction.Length();
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

            if (distance <= moveSpeed * dt)
            {
                Position = targetPosition;
                isMoving = false;
                _movementProgress = 1f;

                state = state switch
                {
                    MinerState.WalkFront => MinerState.IdleFront,
                    MinerState.WalkBack => MinerState.IdleBack,
                    MinerState.WalkLeft => MinerState.IdleLeft,
                    MinerState.WalkRight => MinerState.IdleRight,
                    _ => state
                };

                currentFrame = 0;
                animationTimer = 0f;
            }
            else
            {
                Position += Vector2.Normalize(direction) * moveSpeed * dt;
                _movementProgress = 1f - (direction.Length() / TileMap.TileSize);
            }
        }

        UpdateAnimation(gameTime);
    }

    private void UpdateAnimation(GameTime gameTime)
    {
        string newAnimation = GetAnimationName(state);

        if (newAnimation != currentAnimation)
        {
            currentAnimation = newAnimation;
            currentAnimationFrames = animations.GetValueOrDefault(newAnimation);
            currentFrame = 0;
            animationTimer = 0f;
        }

        if (currentAnimationFrames?.Count > 1)
        {
            animationTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
            if (animationTimer >= animationSpeed)
            {
                animationTimer = 0f;
                currentFrame++;
                if (currentFrame >= currentAnimationFrames.Count)
                    currentFrame = 0;
            }
        }
        else
        {
            currentFrame = 0;
        }
    }

    public void Draw(SpriteBatch spriteBatch)
    {
        if (!animations.TryGetValue(currentAnimation, out var frames) || currentFrame >= frames.Count)
            return;

        var frame = frames[currentFrame];

        for (int i = _bodySegments.Count - 1; i >= 0; i--)
        {
            var (from, to) = _bodySegments[i];
            Vector2 segPos = Vector2.Lerp(from, to, _movementProgress);
            spriteBatch.Draw(texture, segPos, frame, Color.White * 0.8f);
        }

        spriteBatch.Draw(texture, Position, frame, Color.White);
    }

    public Collision GetBounds()
    {
        if (!animations.TryGetValue(currentAnimation, out var frames) || frames.Count == 0)
            return Collision.Empty;

        var frame = frames[Math.Min(currentFrame, frames.Count - 1)];

        return new Collision(
            (int)(Position.X + frame.Width  * 0.5f),
            (int)(Position.Y + frame.Height * 0.5f),
            (int)(frame.Width * 0.5f)
        );
    }

    private string GetAnimationName(MinerState state) => state switch
    {
        MinerState.WalkFront  => "walk_front",
        MinerState.WalkBack   => "walk_back",
        MinerState.WalkLeft   => "walk_left",
        MinerState.WalkRight  => "walk_right",
        MinerState.IdleFront  => "idle_front",
        MinerState.IdleBack   => "idle_back",
        MinerState.IdleLeft   => "idle_left",
        MinerState.IdleRight  => "idle_right",
        _ => "idle_front"
    };
}