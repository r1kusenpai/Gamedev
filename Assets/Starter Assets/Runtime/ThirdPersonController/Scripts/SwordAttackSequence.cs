using System;

namespace StarterAssets
{
    // Input and combo timing are independent of the animation graph and frame rate.
    public sealed class SwordAttackSequence
    {
        public enum AttackKind { None, Combo, Held, Running, Air }
        public struct Command
        {
            public AttackKind Kind;
            public int ComboIndex;
        }
        public float HoldThreshold = .25f, ComboResetSeconds = 1f;
        public bool ResponsiveInput;
        public bool SeparateRunningChain;
        public bool AutoCompleteRunningPair;
        public bool RunningComboFinisher;
        int nextRunning;
        public int ComboCount = 3;
        public float LinkProgress = .85f, BufferSeconds = .4f, MovementCancelProgress = .75f;
        float bufferAge;
        public bool HasBufferedAttack => buffered;
        public bool TryCancelRecovery()
        {
            if (!ResponsiveInput || !Active || buffered || repeatHeld || Progress < MovementCancelProgress) return false;
            Active = false; idleSeconds = 0f; pending = false; return true;
        }
        public bool Active { get; private set; }
        public float Progress => Active ? Math.Min(1f, elapsed / duration) : 0f;
        public Command Current { get; private set; }
        bool pending, buffered, repeatHeld;
        float heldSeconds, elapsed, duration = 1f, idleSeconds = 100f;
        int nextCombo;
        Command buffer;

        public void SetDuration(float seconds) { duration = Math.Max(.05f, seconds); elapsed = 0f; }
        public void Reset()
        {
            Active = pending = buffered = repeatHeld = false;
            heldSeconds = elapsed = 0f; idleSeconds = 100f; nextCombo = nextRunning = 0;
            Current = buffer = default; bufferAge = 0f;
        }
        public Command Tick(float delta, bool down, bool up, bool held, bool grounded, bool running)
        {
            if (ResponsiveInput) return TickResponsive(delta, down, up, held, grounded, running);
            delta = Math.Max(0f, delta);
            if (Active) elapsed += delta; else idleSeconds += delta;
            bool finished = Active && elapsed >= duration;
            if (up || !held || !grounded) repeatHeld = false;
            if (down && !pending)
            {
                // An airborne click starts immediately, before the character can land.
                if (!grounded && (!Active || Current.Kind != AttackKind.Air))
                {
                    pending = buffered = repeatHeld = false; heldSeconds = 0f;
                    return Start(new Command { Kind = AttackKind.Air });
                }
                pending = true; heldSeconds = 0f;
            }
            if (pending)
            {
                heldSeconds += delta;
                if (held && !up && grounded && heldSeconds >= HoldThreshold)
                {
                    repeatHeld = true;
                    if (!Active)
                    {
                        pending = buffered = false;
                        return Start(new Command { Kind = AttackKind.Held });
                    }
                }
                if (up || !held)
                {
                    bool shortTap = heldSeconds < HoldThreshold;
                    var command = new Command
                    {
                        Kind = !grounded ? AttackKind.Air : running ? AttackKind.Running : AttackKind.Combo
                    };
                    pending = false; heldSeconds = 0f;
                    // Match the greatsword: releasing a long hold never queues an extra strike.
                    if (shortTap)
                    {
                        if (Active) { buffer = command; buffered = true; }
                        else return Start(command);
                    }
                }
            }
            if (finished)
            {
                Active = false; idleSeconds = 0f;
                if (repeatHeld && held && grounded)
                {
                    pending = buffered = false;
                    return Start(new Command { Kind = AttackKind.Held });
                }
                if (buffered)
                {
                    var command = buffer; buffered = false;
                    // A buffered strike must respect the actual stance when it starts.
                    if (!grounded) command.Kind = AttackKind.Air;
                    else if (command.Kind == AttackKind.Air) command.Kind = running ? AttackKind.Running : AttackKind.Combo;
                    return Start(command);
                }
            }
            return default;
        }
        Command TickResponsive(float delta, bool down, bool up, bool held, bool grounded, bool running)
        {
            delta = Math.Max(0f, delta);
            if (Active) elapsed += delta; else idleSeconds += delta;
            if (buffered) {
                bool waitingForPairCompletion=AutoCompleteRunningPair && Active && Current.Kind==AttackKind.Running;
                if(!waitingForPairCompletion)bufferAge += delta;
                if (bufferAge > BufferSeconds) buffered = false;
            }
            if (up || !held || !grounded) repeatHeld = false;
            if (down)
            {
                pending = true; heldSeconds = 0f;
                var light = new Command { Kind = !grounded ? AttackKind.Air
                    : running && (SeparateRunningChain || !Active) ? AttackKind.Running : AttackKind.Combo };
                if (!Active) return Start(light);
                buffer = light; buffered = true; bufferAge = 0f;
            }
            if (pending)
            {
                if (held && grounded && !up)
                {
                    heldSeconds += delta;
                    if (heldSeconds >= HoldThreshold) { repeatHeld = true; buffered = false; }
                }
                else { pending = false; heldSeconds = 0f; }
            }
            bool finishRunningPair=AutoCompleteRunningPair && SeparateRunningChain && Active
                && Current.Kind==AttackKind.Running && Current.ComboIndex==0 && grounded;
            if(finishRunningPair && Progress>=LinkProgress) {
                // The second authored strike belongs to this press. Preserve one queued press for the next pair.
                bufferAge=0f;pending=false;repeatHeld=false;
                return Start(new Command { Kind=AttackKind.Running });
            }
            if (Active && (elapsed >= duration || Progress >= LinkProgress && (buffered || repeatHeld)))
            {
                Active = false; idleSeconds = 0f;
                if (repeatHeld && held && grounded)
                {
                    buffered = false; return Start(new Command { Kind = AttackKind.Held });
                }
                if (buffered)
                {
                    var next = buffer; buffered = false;
                    next.Kind = !grounded ? AttackKind.Air : next.Kind == AttackKind.Air
                        ? (running ? AttackKind.Running : AttackKind.Combo) : next.Kind;
                    if (SeparateRunningChain && grounded) next.Kind = running ? AttackKind.Running : AttackKind.Combo;
                    return Start(next);
                }
            }
            return default;
        }
        Command Start(Command command)
        {
            if (idleSeconds > ComboResetSeconds) nextCombo = nextRunning = 0;
            if (SeparateRunningChain && command.Kind == AttackKind.Running)
            { command.ComboIndex = nextRunning; nextRunning = (nextRunning + 1) % (RunningComboFinisher ? 3 : 2); nextCombo = 0; }
            else if (command.Kind == AttackKind.Combo)
            {
                nextRunning = 0;
                command.ComboIndex = nextCombo;
                nextCombo = (nextCombo + 1) % Math.Max(1, ComboCount);
            }
            else nextCombo = nextRunning = 0;
            Current = command; Active = true; elapsed = 0f; idleSeconds = 0f;
            return command;
        }
    }
}

