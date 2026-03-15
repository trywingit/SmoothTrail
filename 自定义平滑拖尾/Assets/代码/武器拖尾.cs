using UnityEngine;
using System.Collections.Generic;
using System;

public class 武器拖尾 : MonoBehaviour
{
    [Header("定位点设置")]
    public Transform m_起始点;
    public Transform m_结束点;

    [Header("拖尾效果参数")]
    public float m_拖尾持续时间 = 0.3f;
    public float m_更新间隔 = 0.02f;
    public float m_最小距离间隔 = 0.1f;
    public Material m_拖尾材质;

    [Header("颜色渐变")]
    public Color m_起始颜色 = Color.white;
    public Color m_结束颜色 = new Color(1, 1, 1, 0);

    [Header("Catmull-Rom插值设置")]
    public bool m_启用插值 = true;
    [Range(2, 10)] public int m_插值分段数 = 4;
    public float m_插值张力 = 0.5f;

    // 内部变量
    private Mesh m_网格;
    private MeshRenderer m_网格渲染器;
    public List<拖尾段> m_拖尾段列表 = new List<拖尾段>();
    private float m_最后更新时间;
    public bool m_是否激活拖尾 = false;

    // 位置记录
    private Vector3 m_最后起始位置;
    private Vector3 m_最后结束位置;

    // 性能优化：对象池
    private List<Vector3> m_顶点池 = new List<Vector3>();
    private List<Color> m_颜色池 = new List<Color>();
    private List<Vector2> m_UV池 = new List<Vector2>();
    private List<int> m_三角形池 = new List<int>();

    [Serializable]
    public class 拖尾段
    {
        public Vector3 起始位置;
        public Vector3 结束位置;
        public float 创建时间;

        public 拖尾段(Vector3 tmp_起始点, Vector3 tmp_结束点, float tmp_时间)
        {
            起始位置 = tmp_起始点;
            结束位置 = tmp_结束点;
            创建时间 = tmp_时间;
        }
    }

    void Awake()
    {
        初始化组件();
    }

    private void Start()
    {
        开始拖尾();
    }

    void 初始化组件()
    {
        MeshFilter tmp_网格过滤器 = GetComponent<MeshFilter>();
        if (tmp_网格过滤器 == null)
            tmp_网格过滤器 = gameObject.AddComponent<MeshFilter>();

        m_网格渲染器 = GetComponent<MeshRenderer>();
        if (m_网格渲染器 == null)
            m_网格渲染器 = gameObject.AddComponent<MeshRenderer>();

        m_网格 = new Mesh();
        m_网格.name = "WeaponTrailMesh";
        tmp_网格过滤器.mesh = m_网格;

        if (m_拖尾材质 != null)
            m_网格渲染器.material = m_拖尾材质;

        m_网格渲染器.enabled = false;
    }

    void Update()
    {
        if (!m_是否激活拖尾 || m_起始点 == null || m_结束点 == null)
            return;

        // 优化：同时检查时间间隔和距离间隔
        bool tmp_是否应该更新 = (Time.time - m_最后更新时间 >= m_更新间隔) &&
                           (Vector3.Distance(m_起始点.position, m_最后起始位置) > m_最小距离间隔 ||
                            Vector3.Distance(m_结束点.position, m_最后结束位置) > m_最小距离间隔);

        if (tmp_是否应该更新)
        {
            添加拖尾段();
            m_最后更新时间 = Time.time;

            // 更新记录的位置
            m_最后起始位置 = m_起始点.position;
            m_最后结束位置 = m_结束点.position;
        }

        更新网格();
        清理过期段();
    }

    void 添加拖尾段()
    {
        Vector3 tmp_当前起始点 = m_起始点.position;
        Vector3 tmp_当前结束点 = m_结束点.position;

        拖尾段 tmp_新段 = new 拖尾段(tmp_当前起始点, tmp_当前结束点, Time.time);
        m_拖尾段列表.Insert(0, tmp_新段);
    }

    void 更新网格()
    {
        if (m_拖尾段列表.Count < 2)
        {
            m_网格.Clear();
            return;
        }

        if (m_启用插值 && m_拖尾段列表.Count >= 2)
        {
            生成插值网格();
        }
        else
        {
            生成原始网格();
        }
    }

    void 生成原始网格()
    {
        int tmp_段数量 = m_拖尾段列表.Count;
        int tmp_顶点数量 = tmp_段数量 * 2;

        // 使用对象池避免频繁内存分配
        确保列表容量(m_顶点池, tmp_顶点数量);
        确保列表容量(m_颜色池, tmp_顶点数量);
        确保列表容量(m_UV池, tmp_顶点数量);
        确保列表容量(m_三角形池, (tmp_段数量 - 1) * 6);

        m_顶点池.Clear();
        m_颜色池.Clear();
        m_UV池.Clear();
        m_三角形池.Clear();

        for (int tmp_索引 = 0; tmp_索引 < tmp_段数量; tmp_索引++)
        {
            拖尾段 tmp_段 = m_拖尾段列表[tmp_索引];
            float tmp_生命比率 = (Time.time - tmp_段.创建时间) / m_拖尾持续时间;
            float tmp_U值 = 1.0f - (float)tmp_索引 / (tmp_段数量 - 1);

            m_顶点池.Add(tmp_段.起始位置);
            m_顶点池.Add(tmp_段.结束位置);

            Color tmp_段颜色 = Color.Lerp(m_起始颜色, m_结束颜色, tmp_生命比率);
            m_颜色池.Add(tmp_段颜色);
            m_颜色池.Add(tmp_段颜色);

            m_UV池.Add(new Vector2(tmp_U值, 0));
            m_UV池.Add(new Vector2(tmp_U值, 1));
        }

        for (int tmp_索引 = 0; tmp_索引 < tmp_段数量 - 1; tmp_索引++)
        {
            int tmp_顶点索引 = tmp_索引 * 2;

            m_三角形池.Add(tmp_顶点索引);
            m_三角形池.Add(tmp_顶点索引 + 2);
            m_三角形池.Add(tmp_顶点索引 + 1);

            m_三角形池.Add(tmp_顶点索引 + 1);
            m_三角形池.Add(tmp_顶点索引 + 2);
            m_三角形池.Add(tmp_顶点索引 + 3);
        }

        应用网格数据();
    }

    void 生成插值网格()
    {
        int tmp_原始段数量 = m_拖尾段列表.Count;

        // 提取起始点和结束点序列
        Vector3[] tmp_起始点序列 = new Vector3[tmp_原始段数量];
        Vector3[] tmp_结束点序列 = new Vector3[tmp_原始段数量];
        float[] tmp_时间序列 = new float[tmp_原始段数量];

        for (int tmp_索引 = 0; tmp_索引 < tmp_原始段数量; tmp_索引++)
        {
            tmp_起始点序列[tmp_索引] = m_拖尾段列表[tmp_索引].起始位置;
            tmp_结束点序列[tmp_索引] = m_拖尾段列表[tmp_索引].结束位置;
            tmp_时间序列[tmp_索引] = m_拖尾段列表[tmp_索引].创建时间;
        }

        // 应用Catmull-Rom插值[2](@ref)
        List<Vector3> tmp_插值起始点 = 应用CatmullRom插值(tmp_起始点序列);
        List<Vector3> tmp_插值结束点 = 应用CatmullRom插值(tmp_结束点序列);
        List<float> tmp_插值时间 = 应用CatmullRom插值(tmp_时间序列);

        int tmp_插值点数量 = tmp_插值起始点.Count;

        // 使用对象池准备数据
        int tmp_顶点数量 = tmp_插值点数量 * 2;
        确保列表容量(m_顶点池, tmp_顶点数量);
        确保列表容量(m_颜色池, tmp_顶点数量);
        确保列表容量(m_UV池, tmp_顶点数量);
        确保列表容量(m_三角形池, (tmp_插值点数量 - 1) * 6);

        m_顶点池.Clear();
        m_颜色池.Clear();
        m_UV池.Clear();
        m_三角形池.Clear();

        for (int tmp_索引 = 0; tmp_索引 < tmp_插值点数量; tmp_索引++)
        {
            float tmp_生命比率 = (Time.time - tmp_插值时间[tmp_索引]) / m_拖尾持续时间;
            float tmp_U值 = 1.0f - (float)tmp_索引 / (tmp_插值点数量 - 1);

            m_顶点池.Add(tmp_插值起始点[tmp_索引]);
            m_顶点池.Add(tmp_插值结束点[tmp_索引]);

            Color tmp_段颜色 = Color.Lerp(m_起始颜色, m_结束颜色, tmp_生命比率);
            m_颜色池.Add(tmp_段颜色);
            m_颜色池.Add(tmp_段颜色);

            m_UV池.Add(new Vector2(tmp_U值, 0));
            m_UV池.Add(new Vector2(tmp_U值, 1));
        }

        for (int tmp_索引 = 0; tmp_索引 < tmp_插值点数量 - 1; tmp_索引++)
        {
            int tmp_顶点索引 = tmp_索引 * 2;

            m_三角形池.Add(tmp_顶点索引);
            m_三角形池.Add(tmp_顶点索引 + 2);
            m_三角形池.Add(tmp_顶点索引 + 1);

            m_三角形池.Add(tmp_顶点索引 + 1);
            m_三角形池.Add(tmp_顶点索引 + 2);
            m_三角形池.Add(tmp_顶点索引 + 3);
        }

        应用网格数据();
    }

    List<Vector3> 应用CatmullRom插值(Vector3[] tmp_点序列)
    {
        List<Vector3> tmp_结果 = new List<Vector3>();
        int tmp_点数量 = tmp_点序列.Length;

        if (tmp_点数量 < 2) return tmp_结果;

        for (int tmp_索引 = 0; tmp_索引 < tmp_点数量 - 1; tmp_索引++)
        {
            // 获取四个控制点[2](@ref)
            Vector3 tmp_P0 = tmp_索引 > 0 ? tmp_点序列[tmp_索引 - 1] : tmp_点序列[0];
            Vector3 tmp_P1 = tmp_点序列[tmp_索引];
            Vector3 tmp_P2 = tmp_点序列[tmp_索引 + 1];
            Vector3 tmp_P3 = tmp_索引 < tmp_点数量 - 2 ? tmp_点序列[tmp_索引 + 2] : tmp_点序列[tmp_点数量 - 1];

            // 添加当前点
            tmp_结果.Add(tmp_P1);

            // 在P1和P2之间插入点[2](@ref)
            for (int tmp_分段 = 1; tmp_分段 <= m_插值分段数; tmp_分段++)
            {
                float tmp_插值参数 = (float)tmp_分段 / (m_插值分段数 + 1);
                Vector3 tmp_插值点 = 计算CatmullRom点(tmp_P0, tmp_P1, tmp_P2, tmp_P3, tmp_插值参数);
                tmp_结果.Add(tmp_插值点);
            }
        }

        // 添加最后一个点
        tmp_结果.Add(tmp_点序列[tmp_点数量 - 1]);

        return tmp_结果;
    }

    List<float> 应用CatmullRom插值(float[] tmp_值序列)
    {
        List<float> tmp_结果 = new List<float>();
        int tmp_数量 = tmp_值序列.Length;

        if (tmp_数量 < 2) return tmp_结果;

        for (int tmp_索引 = 0; tmp_索引 < tmp_数量 - 1; tmp_索引++)
        {
            float tmp_P0 = tmp_索引 > 0 ? tmp_值序列[tmp_索引 - 1] : tmp_值序列[0];
            float tmp_P1 = tmp_值序列[tmp_索引];
            float tmp_P2 = tmp_值序列[tmp_索引 + 1];
            float tmp_P3 = tmp_索引 < tmp_数量 - 2 ? tmp_值序列[tmp_索引 + 2] : tmp_值序列[tmp_数量 - 1];

            tmp_结果.Add(tmp_P1);

            for (int tmp_分段 = 1; tmp_分段 <= m_插值分段数; tmp_分段++)
            {
                float tmp_插值参数 = (float)tmp_分段 / (m_插值分段数 + 1);
                float tmp_插值点 = 计算CatmullRom点(tmp_P0, tmp_P1, tmp_P2, tmp_P3, tmp_插值参数);
                tmp_结果.Add(tmp_插值点);
            }
        }

        tmp_结果.Add(tmp_值序列[tmp_数量 - 1]);
        return tmp_结果;
    }

    Vector3 计算CatmullRom点(Vector3 tmp_P0, Vector3 tmp_P1, Vector3 tmp_P2, Vector3 tmp_P3, float tmp_插值参数)
    {
        // Catmull-Rom样条插值公式[2](@ref)
        float tmp_参数平方 = tmp_插值参数 * tmp_插值参数;
        float tmp_参数立方 = tmp_参数平方 * tmp_插值参数;

        return 0.5f * (
            (2f * tmp_P1) +
            (-tmp_P0 + tmp_P2) * tmp_插值参数 +
            (2f * tmp_P0 - 5f * tmp_P1 + 4f * tmp_P2 - tmp_P3) * tmp_参数平方 +
            (-tmp_P0 + 3f * tmp_P1 - 3f * tmp_P2 + tmp_P3) * tmp_参数立方
        );
    }

    float 计算CatmullRom点(float tmp_P0, float tmp_P1, float tmp_P2, float tmp_P3, float tmp_插值参数)
    {
        float tmp_参数平方 = tmp_插值参数 * tmp_插值参数;
        float tmp_参数立方 = tmp_参数平方 * tmp_插值参数;

        return 0.5f * (
            (2f * tmp_P1) +
            (-tmp_P0 + tmp_P2) * tmp_插值参数 +
            (2f * tmp_P0 - 5f * tmp_P1 + 4f * tmp_P2 - tmp_P3) * tmp_参数平方 +
            (-tmp_P0 + 3f * tmp_P1 - 3f * tmp_P2 + tmp_P3) * tmp_参数立方
        );
    }

    void 应用网格数据()
    {
        m_网格.Clear();
        m_网格.vertices = m_顶点池.ToArray();
        m_网格.colors = m_颜色池.ToArray();
        m_网格.uv = m_UV池.ToArray();
        m_网格.triangles = m_三角形池.ToArray();
        m_网格.RecalculateNormals();
        m_网格.RecalculateBounds();
    }

    void 确保列表容量<T>(List<T> tmp_列表, int tmp_所需容量)
    {
        if (tmp_列表.Capacity < tmp_所需容量)
        {
            tmp_列表.Capacity = Mathf.NextPowerOfTwo(tmp_所需容量);
        }
    }

    void 清理过期段()
    {
        float tmp_当前时间 = Time.time;
        for (int tmp_索引 = m_拖尾段列表.Count - 1; tmp_索引 >= 0; tmp_索引--)
        {
            if (tmp_当前时间 - m_拖尾段列表[tmp_索引].创建时间 > m_拖尾持续时间)
            {
                m_拖尾段列表.RemoveAt(tmp_索引);
            }
        }
    }

    public void 开始拖尾()
    {
        if (m_起始点 == null || m_结束点 == null)
        {
            Debug.LogError("WeaponTrailRenderer: 起始点或结束点未分配!");
            return;
        }

        m_是否激活拖尾 = true;
        m_拖尾段列表.Clear();
        m_最后更新时间 = Time.time;

        // 初始化记录的位置
        m_最后起始位置 = m_起始点.position;
        m_最后结束位置 = m_结束点.position;

        if (m_网格渲染器 != null)
            m_网格渲染器.enabled = true;
    }

    public void 停止拖尾()
    {
        m_是否激活拖尾 = false;
    }

    public void 立即清理拖尾()
    {
        m_是否激活拖尾 = false;
        m_拖尾段列表.Clear();
        if (m_网格 != null)
            m_网格.Clear();
        if (m_网格渲染器 != null)
            m_网格渲染器.enabled = false;
    }
}