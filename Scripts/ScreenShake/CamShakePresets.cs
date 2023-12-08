using Godot;
using System;
using System.Linq.Expressions;

public static class CamShakePresets
{
    public static CamShakeInstance Idle
    {
        get 
        {
            CamShakeInstance c = new CamShakeInstance(0.01f, 0.05f, 50f, 0.1f);
            c.maxTrauma = 0.3f;
            c.loop = true;

            return c;
        }
    }

    public static CamShakeInstance Step
    {
        get
        {
            CamShakeInstance c = new CamShakeInstance(0.1f, 0.1f, 10f, 0.5f, 1.5f);

            return c;
        }
    }

    public static CamShakeInstance Sprinting
    {
        get
        {
            CamShakeInstance c = new CamShakeInstance(0.1f, 0.1f, 15f, 0.1f);
            c.maxTrauma = 0.3f;
            c.loop = true;

            return c;
        }
    }

    public static CamShakeInstance Walking
    {
        get
        {
            CamShakeInstance c = new CamShakeInstance(0.1f, 0.1f, 5f, 0.1f);
            c.maxTrauma = 0.3f;
            c.loop = true;

            return c;
        }
    }

    public static CamShakeInstance InAir
    {
        get
        {
            CamShakeInstance c = new CamShakeInstance(0.1f, 0.1f, 15f, 0.3f);
            c.maxTrauma = 0.55f;
            c.loop = true;

            return c;
        }
    }

    public static CamShakeInstance Sliding
    {
        get
        {
            CamShakeInstance c = new CamShakeInstance(0.1f, 0.1f, 25f, 0.3f);
            c.maxTrauma = 0.7f;
            c.loop = true;

            return c;
        }
    }

    public static CamShakeInstance Roll
    {
        get
        {
            CamShakeInstance c = new CamShakeInstance(0.1f, 0.5f, 50f, 1f, 0.5f);

            return c;
        }
    }

    public static CamShakeInstance Vault
    {
        get
        {
            CamShakeInstance c = new CamShakeInstance(0.2f, 0.5f, 25f, 0.8f, 0.5f);

            return c;
        }
    }
}
