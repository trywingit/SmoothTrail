using System.Buffers;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

/// <summary>
/// 自定义拖尾渲染器 - 使用Catmull-Rom算法实现平滑轨迹（对象池优化版）
/// </summary>
public class 自定义拖尾优化 : MonoBehaviour
{
    [Header("拖尾参数")]
    [SerializeField] private int m_最大轨迹点数 = 50;
    [SerializeField] private float m_点间距阈值 = 0.1f;
    [SerializeField] private int m_插值精度 = 10;
    [SerializeField] private Color m_拖尾颜色 = Color.white;
    [SerializeField] private float m_拖尾宽度 = 0.1f;
    [SerializeField] private float m_最大存在时间 = 2.0f;
    [SerializeField] private bool m_是否暂停计时 = false;
    [SerializeField] private float m_张力参数 = 0.5f;

    // 组件引用
    public LineRenderer m_拖尾渲染器;
    private List<拖尾点数据> m_轨迹点列表 = new List<拖尾点数据>();

    // 对象池相关
    private ObjectPool<拖尾点数据> m_点数据池;
    private List<Vector3> m_临时路径点列表 = new List<Vector3>();
    private Vector3[] m_重用路径点数组;

    // 暂停相关变量
    private float m_暂停开始时间 = 0f;
    private bool m_内部暂停状态 = false;

    public int m_更新间隔帧 = 1;

    private static readonly ArrayPool<Vector3> s_向量数组池 = ArrayPool<Vector3>.Shared;

    /// <summary>
    /// 拖尾点数据结构，包含位置和时间信息
    /// </summary>
    private class 拖尾点数据
    {
        public Vector3 位置;
        public float 生成时间;
        public float 已存活时间;

        public 拖尾点数据()
        {
            // 默认构造函数用于对象池
        }

        public void 初始化(Vector3 位置, float 生成时间)
        {
            this.位置 = 位置;
            this.生成时间 = 生成时间;
            this.已存活时间 = 0f;
        }

        public void 重置()
        {
            // 清理数据，准备重用
            位置 = Vector3.zero;
            生成时间 = 0f;
            已存活时间 = 0f;
        }
    }

    void Awake()
    {
        // 初始化对象池
        初始化对象池();
        // 初始化拖尾渲染器
        初始化拖尾渲染器();
    }

    void OnDestroy()
    {
        // 销毁时清理对象池
        if (m_点数据池 != null)
        {
            m_点数据池.Dispose();
        }
    }

    void Update()
    {
        if(Time.frameCount % m_更新间隔帧 == 0)
        {
            // 更新暂停状态
            更新暂停状态();

            // 更新轨迹点
            更新轨迹点();

            // 生成平滑轨迹
            if (m_轨迹点列表.Count >= 2)
            {
                生成平滑轨迹();
            }
            else if (m_轨迹点列表.Count == 1)
            {
                // 只有一个点时直接显示
                m_拖尾渲染器.positionCount = 1;
                m_拖尾渲染器.SetPosition(0, m_轨迹点列表[0].位置);
            }
            else
            {
                m_拖尾渲染器.positionCount = 0;
            }
        }

    }

    /// <summary>
    /// 初始化对象池
    /// </summary>
    private void 初始化对象池()
    {
        m_点数据池 = new ObjectPool<拖尾点数据>(
            createFunc: () => new 拖尾点数据(),
            actionOnGet: (点数据) => { }, 
            actionOnRelease: (点数据) => 点数据.重置(), 
            actionOnDestroy: (点数据) => { }, 
            collectionCheck: false, 
            defaultCapacity: m_最大轨迹点数,
            maxSize: m_最大轨迹点数 * 2 
        );

        // 预分配临时列表容量
        m_临时路径点列表.Capacity = m_最大轨迹点数 * m_插值精度;
        m_重用路径点数组 = new Vector3[m_最大轨迹点数 * m_插值精度];
    }

    /// <summary>
    /// 初始化LineRenderer组件
    /// </summary>
    private void 初始化拖尾渲染器()
    {

        m_拖尾渲染器.useWorldSpace = true;
        m_拖尾渲染器.positionCount = 0;

    }

    /// <summary>
    /// 更新暂停状态
    /// </summary>
    private void 更新暂停状态()
    {
        if (m_是否暂停计时 && !m_内部暂停状态)
        {
            m_内部暂停状态 = true;
            m_暂停开始时间 = Time.time;
        }
        else if (!m_是否暂停计时 && m_内部暂停状态)
        {
            m_内部暂停状态 = false;
            float tmp_暂停持续时间 = Time.time - m_暂停开始时间;

            foreach (var tmp_点 in m_轨迹点列表)
            {
                tmp_点.生成时间 += tmp_暂停持续时间;
            }
        }
    }

    /// <summary>
    /// 更新轨迹点列表（使用对象池优化）
    /// </summary>
    private void 更新轨迹点()
    {
        Vector3 tmp_当前位置 = transform.position;
        float tmp_当前时间 = Time.time;

        // 如果列表为空或距离足够远，添加新点
        if (m_轨迹点列表.Count == 0 ||  (tmp_当前位置 - m_轨迹点列表[m_轨迹点列表.Count - 1].位置).sqrMagnitude > m_点间距阈值 * m_点间距阈值)
        {

            var 新点数据 = m_点数据池.Get();
            新点数据.初始化(tmp_当前位置, tmp_当前时间);
            m_轨迹点列表.Add(新点数据);


            while (m_轨迹点列表.Count > m_最大轨迹点数)
            {
                var 旧点 = m_轨迹点列表[0];
                m_轨迹点列表.RemoveAt(0);
                m_点数据池.Release(旧点); // 放回对象池
            }
        }

        // 更新所有点的存活时间并移除过期点
        更新点存活时间并移除过期点();
    }



    /// <summary>
    /// 更新点存活时间并移除过期点（使用对象池优化）
    /// </summary>
    private void 更新点存活时间并移除过期点()
    {
        // 更新存活时间并移除过期点
        for (int i = m_轨迹点列表.Count - 1; i >= 0; i--)
        {
            var tmp_点 = m_轨迹点列表[i];

            if (!m_内部暂停状态)
            {
                tmp_点.已存活时间 = Time.time - tmp_点.生成时间;
            }

            if (tmp_点.已存活时间 > m_最大存在时间)
            {
                var 过期点 = m_轨迹点列表[i];
                m_轨迹点列表.RemoveAt(i);
                m_点数据池.Release(过期点); // 放回对象池
            }
        }
    }

    /// <summary>
    /// 使用Catmull-Rom算法生成平滑轨迹（优化内存分配）
    /// </summary>
    private void 生成平滑轨迹()
    {
        // 准备控制点
        Vector3[] tmp_控制点数组 = 准备控制点();

        // 清空临时列表（重用已分配的内存）
        m_临时路径点列表.Clear();

        // 生成平滑路径点
        for (int i = 0; i < tmp_控制点数组.Length - 3; i++)
        {
            Vector3 tmp_点0 = tmp_控制点数组[i];
            Vector3 tmp_点1 = tmp_控制点数组[i + 1];
            Vector3 tmp_点2 = tmp_控制点数组[i + 2];
            Vector3 tmp_点3 = tmp_控制点数组[i + 3];

            for (int j = 0; j <= m_插值精度; j++)
            {
                float tmp_插值比例 = j / (float)m_插值精度;
                Vector3 tmp_插值点 = 计算CatmullRom点(tmp_点0, tmp_点1, tmp_点2, tmp_点3, tmp_插值比例);
                m_临时路径点列表.Add(tmp_插值点);
            }
        }

        m_临时路径点列表.Reverse();

        if (m_重用路径点数组.Length < m_临时路径点列表.Count)
        {
            m_重用路径点数组 = new Vector3[m_临时路径点列表.Count];
        }

        // 直接复制到重用数组
        for (int i = 0; i < m_临时路径点列表.Count; i++)
        {
            m_重用路径点数组[i] = m_临时路径点列表[i];
        }

        // 更新LineRenderer
        m_拖尾渲染器.positionCount = m_临时路径点列表.Count;
        m_拖尾渲染器.SetPositions(m_重用路径点数组);
    }

    /// <summary>
    /// 为Catmull-Rom插值准备控制点
    /// </summary>
    private Vector3[] 准备控制点()
    {
        if (m_轨迹点列表.Count < 2)
            return new Vector3[0];

        // 重用临时列表来构建控制点
        m_临时路径点列表.Clear();

        // 添加起始虚拟点
        Vector3 tmp_起始虚拟点 = m_轨迹点列表[0].位置 +
            (m_轨迹点列表[0].位置 - m_轨迹点列表[1].位置);
        m_临时路径点列表.Add(tmp_起始虚拟点);

        // 添加原始点
        foreach (var tmp_点 in m_轨迹点列表)
        {
            m_临时路径点列表.Add(tmp_点.位置);
        }

        // 添加结束虚拟点
        Vector3 tmp_结束虚拟点 = m_轨迹点列表[m_轨迹点列表.Count - 1].位置 +
            (m_轨迹点列表[m_轨迹点列表.Count - 1].位置 - m_轨迹点列表[m_轨迹点列表.Count - 2].位置);
        m_临时路径点列表.Add(tmp_结束虚拟点);

        return m_临时路径点列表.ToArray();
    }

    /// <summary>
    /// Catmull-Rom插值核心算法
    /// </summary>
    private Vector3 计算CatmullRom点(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {

        float s = (1.0f - m_张力参数) * 0.5f;
        float t2 = t * t;
        float t3 = t2 * t;

        Vector3 result = (2 * p1) * s;
        result += ((-p0 + p2) * s) * t;
        result += ((2 * p0 - 5 * p1 + 4 * p2 - p3) * s) * t2;
        result += ((-p0 + 3 * p1 - 3 * p2 + p3) * s) * t3;

        return result;

    }

    /// <summary>
    /// 清空拖尾轨迹（使用对象池优化）
    /// </summary>
    public void 清空拖尾()
    {
        foreach (var 点 in m_轨迹点列表)
        {
            m_点数据池.Release(点);
        }
        m_轨迹点列表.Clear();

        if (m_拖尾渲染器 != null)
        {
            m_拖尾渲染器.positionCount = 0;
        }
    }

    /// <summary>
    /// 设置拖尾颜色
    /// </summary>
    public void 设置颜色(Color 新颜色)
    {
        m_拖尾颜色 = 新颜色;
        if (m_拖尾渲染器 != null)
        {
            m_拖尾渲染器.startColor = m_拖尾颜色;
            m_拖尾渲染器.endColor = m_拖尾颜色;
        }
    }

    /// <summary>
    /// 设置拖尾宽度
    /// </summary>
    public void 设置宽度(float 新宽度)
    {
        m_拖尾宽度 = 新宽度;
        if (m_拖尾渲染器 != null)
        {
            m_拖尾渲染器.startWidth = m_拖尾宽度;
            m_拖尾渲染器.endWidth = m_拖尾宽度;
        }
    }

    /// <summary>
    /// 设置是否暂停计时
    /// </summary>
    public void 设置暂停(bool 是否暂停)
    {
        m_是否暂停计时 = 是否暂停;
    }

    /// <summary>
    /// 强制恢复拖尾计时
    /// </summary>
    public void 强制恢复计时()
    {
        m_是否暂停计时 = false;
        m_内部暂停状态 = false;

        float tmp_当前时间 = Time.time;
        foreach (var tmp_点 in m_轨迹点列表)
        {
            tmp_点.已存活时间 = tmp_当前时间 - tmp_点.生成时间;
        }
    }
}