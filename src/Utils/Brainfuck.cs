using System;
using System.Text;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using PacManBot.Extensions;

namespace PacManBot.Utils
{
    /// <summary>
    /// Represents a Brainfuck program and allows its execution. <para />
    /// This implementation's details are: <para />
    /// 1. Byte value overflow/underflow on 0-- and 255++ <para/>
    /// 2. Runtime error on out-of-bounds memory access. <para />
    /// 3. Unchanged cells during and after an EOF on input. <para />
    /// 4. An added # instruction for debugging. <para />
    /// </summary>
    public class Brainfuck
    {
        /// <summary>
        /// A runtime excepton thrown by a Brainfuck program.
        /// </summary>
        public class BrainfuckException : Exception
        {
            /// <summary>The program that threw the exception.</summary>
            public Brainfuck Program { get; }
            /// <summary>The memory of the program at the point of the exception.</summary>
            public string Memory { get; }
            /// <summary>The index of the instruction where the exception occured.</summary>
            public int InstructionIndex { get; }
            /// <summary>The instruction where the exception occured.</summary>
            public char Instruction { get; }

            public BrainfuckException(string message, Brainfuck program, Exception innerException = null)
                : base($"{message} at index {program.instrPtr}", innerException)
            {
                Program = program;
                Memory = Program.MemoryAsString();
                InstructionIndex = Program.instrPtr;
                Instruction = Program.CurrentInstr;
            }
        }



        /// <summary>Upper bound for a brainfuck program's memory size.</summary>
        public const int MaxMemorySize = 10_000;

        private readonly string instr;
        private readonly List<byte> memory;
        private readonly List<byte> output;
        private IReadOnlyList<byte> input;
        private int instrPtr;
        private int memPtr;
        private int inputPtr;

        private CancellationTokenSource cts = new CancellationTokenSource();

        private char CurrentInstr => instr[instrPtr];
        private byte CurrentValue { get => memory[memPtr]; set => memory[memPtr] = value; }
        private byte CurrentInput => input[inputPtr];

        public bool Finished => instrPtr >= instr.Length - 1;
        public string Output => Encoding.ASCII.GetString(output.ToArray());
        public string RawOutput => string.Join(' ', output);

        public override string ToString() => Output;


        /// <summary>Creates a new Brainfuck program ready for execution.</summary>
        /// <param name="program">The program's code. Gets minified automatically.</param>
        /// <param name="programInput">The ASCII input of the program.</param>
        /// <param name="initialMemorySize">The initial memory size in bytes of the program. Can't exceed <see cref="MaxMemorySize"/>.</param>
        public Brainfuck(string program, string programInput = "", int initialMemorySize = 128)
        {
            program = Minify(program ?? throw new ArgumentNullException(nameof(program)));

            if (initialMemorySize > MaxMemorySize)
                throw new ArgumentOutOfRangeException(nameof(initialMemorySize));
            if (!HasMatchingJumps(program))
                throw new ArgumentException("Unmatched jumps in program");

            instr = program;
            memory = new List<byte>(initialMemorySize);
            output = new List<byte>(64);

            Reset(programInput ?? "");
        }


        /// <summary>Resets the program to be run again from the beginning.</summary>
        public void Reset(string newInput = null)
        {
            memory.Clear();
            memory.Add(0);

            output.Clear();
            if (newInput != null) input = Encoding.ASCII.GetBytes(newInput);

            instrPtr = -1;
            memPtr = 0;
            inputPtr = 0;
        }


        /// <summary>Interprets the entire program asynchronously with a timeout.</summary>
        public async Task<Brainfuck> RunAsync(int timeout)
        {
            var task = Task.Run(() => { while (RunStep()) ; });
            if (await Task.WhenAny(task, Task.Delay(timeout)) != task) cts.Cancel();
            await task;

            return this;
        }

        /// <summary>Interprets a single instruction in the program and returns whether the program should continue.</summary>
        public bool RunStep()
        {
            if (Finished) return false;

            try { ReadNext(); }
            catch (Exception e)
            {
                if (!(e is BrainfuckException)) e = new BrainfuckException(e.Message, this, e);
                instrPtr = instr.Length; // Acts as though finished
                throw e;
            }

            return !Finished;
        }


        private void ReadNext()
        {
            if (cts.Token.IsCancellationRequested) throw new BrainfuckException("Program halted or took too long", this);

            switch (instr[++instrPtr])
            {
                case '<':
                    if (--memPtr < 0) throw new BrainfuckException("Memory pointer underflow", this);
                    break;

                case '>':
                    if (++memPtr >= MaxMemorySize) throw new BrainfuckException("Memory pointer overflow", this);
                    if (memPtr >= memory.Count) memory.Add(0);
                    break;

                case '+':
                    CurrentValue++;
                    break;

                case '-':
                    CurrentValue--;
                    break;

                case '[':
                    if (CurrentValue == 0) MoveToMatching('[', ']', +1);
                    break;

                case ']':
                    if (CurrentValue != 0) MoveToMatching(']', '[', -1);
                    break;

                case '.':
                    output.Add(CurrentValue);
                    break;

                case ',':
                    if (inputPtr < input.Count) CurrentValue = input[inputPtr++];
                    break;

                case '#':
                    output.AddRange(Encoding.ASCII.GetBytes($"\n{MemoryAsString()}\n"));
                    break;
            }
        }


        private void MoveToMatching(char current, char other, int step)
        {
            int depth = 0;
            while (true)
            {
                instrPtr += step;
                if (CurrentInstr == current) depth++;
                else if (CurrentInstr == other)
                {
                    if (depth == 0) break;
                    else depth--;
                }
            }
        }


        private string MemoryAsString()
        {
            const int limit = 16;
            var m = memory.Take(limit).Select((x, i) => i == memPtr ? $"({x})" : $"{x}").JoinString(" ");
            return $"{{ {m}{"...".If(memory.Count > limit)} }}";
        }




        public static bool HasMatchingJumps(string program)
        {
            int depth = 0;
            for (int i = 0; depth >= 0 && i < program.Length; i++)
            {
                if (program[i] == '[') depth++;
                else if (program[i] == ']') depth--;
            }
            return depth == 0;
        }


        public static string Minify(string program)
        {
            return string.Join("", program.Where(x => "<>[]+-.,#".Contains(x)));
        }
    }
}
