﻿using Microsoft.Extensions.Options;
using SharpVk.Glfw;
using System;

namespace LD38
{
    public class GlfwService
        : GameService, IUpdatable
    {
        private readonly IUpdateLoopService updateLoop;
        private readonly GlfwOptions options;

        private WindowSizeDelegate windowSizeChanged;
        private Game game;

        private bool isResizeSignalled;

        public GlfwService(IUpdateLoopService updateLoop, IOptions<GlfwOptions> options)
        {
            this.updateLoop = updateLoop;
            this.options = options.Value;
        }

        public bool IsResized
        {
            get;
            private set;
        }

        public WindowHandle WindowHandle
        {
            get;
            private set;
        }

        public int WindowWidth
        {
            get;
            private set;
        }

        public int WindowHeight
        {
            get;
            private set;
        }

        public void SignalResize()
        {
            this.isResizeSignalled = true;
        }

        public override void Initialise(Game game)
        {
            Glfw3.glfwInit();

            Glfw3.glfwWindowHint(0x00022001, 0);
            this.WindowHandle = Glfw3.glfwCreateWindow(1280, 720, this.options.Title, IntPtr.Zero, IntPtr.Zero);

            this.windowSizeChanged = this.OnWindowSizeChanged;
            Glfw3.glfwSetWindowSizeCallback(this.WindowHandle, this.windowSizeChanged);

            this.game = game;
        }

        private void OnWindowSizeChanged(WindowHandle window, int width, int height)
        {
            this.WindowWidth = width;
            this.WindowHeight = height;

            this.SignalResize();
        }

        public override void Start()
        {
            this.updateLoop.Register(this, UpdateStage.PreUpdate);
        }

        public void Update()
        {
            Glfw3.glfwPollEvents();

            if (Glfw3.glfwWindowShouldClose(this.WindowHandle))
            {
                this.game.SignalStop();
            }

            this.IsResized = this.isResizeSignalled;

            this.isResizeSignalled = false;
        }

        public override void Stop()
        {
            this.updateLoop.Deregister(this);
        }
    }
}
