<template>
  <div>
    <div class="card">
      <h3 style="margin:0 0 10px 0">报表看板（Demo）</h3>
      <div class="row" style="align-items:end">
        <div class="col" style="max-width:220px">
          <label>年度</label>
          <input type="number" v-model.number="year" />
        </div>
        <div class="col" style="max-width:260px">
          <label>证照到期窗口（天）</label>
          <input type="number" v-model.number="days" />
        </div>
        <div class="col" style="display:flex;align-items:end;gap:10px">
          <button @click="load">刷新</button>
          <span v-if="err" class="bad">{{ err }}</span>
        </div>
      </div>
      <div v-if="loading" style="margin-top:10px;color:#666">加载中...</div>
    </div>

    <div v-if="data">
      <div class="row" style="gap:10px">
        <div class="card" style="flex:1 1 240px">
          <div style="font-size:12px;opacity:.7">{{ year }} 评鉴单</div>
          <div style="font-size:26px;font-weight:800">{{ data.summary.totalAudits }}</div>
          <div style="font-size:12px;opacity:.7">已判级：{{ data.summary.gradedAudits }}；D/E：{{ data.summary.deAudits }}</div>
        </div>

        <div class="card" style="flex:1 1 240px">
          <div style="font-size:12px;opacity:.7">{{ year }} CAPA 闭环</div>
          <div style="font-size:26px;font-weight:800">{{ Math.round(data.summary.closureRate * 100) }}%</div>
          <div style="font-size:12px;opacity:.7">总：{{ data.summary.totalCapa }}；已关闭：{{ data.summary.closedCapa }}；逾期：{{ data.summary.overdueCapa }}</div>
        </div>

        <div class="card" style="flex:1 1 240px">
          <div style="font-size:12px;opacity:.7">风险预警</div>
          <div style="font-size:26px;font-weight:800">逾期整改 {{ data.risk.overdueCapaCount }}</div>
          <div style="font-size:12px;opacity:.7">证照已过期：{{ data.risk.certExpiredCount }}；7天内到期：{{ data.risk.certExpiringSoonCount }}</div>
        </div>
      </div>

      <div class="card">
        <div style="display:flex;justify-content:space-between;align-items:center">
          <h4 style="margin:0">趋势：年度等级分布</h4>
          <span class="badge">按 Audit.FinalGrade 聚合</span>
        </div>
        <div style="overflow:auto;margin-top:10px">
          <table class="t">
            <thead>
              <tr>
                <th>Year</th><th>A</th><th>B</th><th>C</th><th>D</th><th>E</th><th>Total</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="t in data.gradeTrend" :key="t.year">
                <td>{{ t.year }}</td>
                <td>{{ t.a }}</td>
                <td>{{ t.b }}</td>
                <td>{{ t.c }}</td>
                <td>{{ t.d }}</td>
                <td>{{ t.e }}</td>
                <td>{{ t.total }}</td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>

      <div class="card">
        <div style="display:flex;justify-content:space-between;align-items:center">
          <h4 style="margin:0">维度对比：模块不合格条款数（{{ year }}）</h4>
          <span class="badge">Status=2/3 视为不合格</span>
        </div>
        <div style="overflow:auto;margin-top:10px">
          <table class="t">
            <thead>
              <tr>
                <th>模块</th><th>部分不符合</th><th>不符合</th><th>不适用</th><th>合计条款</th><th>不合格合计</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="m in data.moduleCompare" :key="m.moduleId">
                <td>{{ m.moduleName }}</td>
                <td>{{ m.partialFail }}</td>
                <td>{{ m.fail }}</td>
                <td>{{ m.notApplicable }}</td>
                <td>{{ m.total }}</td>
                <td><span class="bad">{{ m.nonconform }}</span></td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>

      <div class="card">
        <div style="display:flex;justify-content:space-between;align-items:center">
          <h4 style="margin:0">证照到期（<= {{ days }} 天，含已过期）</h4>
          <span class="badge">FactoryCertificate</span>
        </div>
        <div style="overflow:auto;margin-top:10px">
          <table class="t">
            <thead>
              <tr>
                <th>工厂</th><th>证照</th><th>证照号</th><th>到期日</th><th>剩余天数</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="c in data.certExpiring" :key="c.factoryCode + c.certName + c.expireDate">
                <td>{{ c.factoryCode }} - {{ c.factoryName }}</td>
                <td>{{ c.certName }}</td>
                <td>{{ c.certNo }}</td>
                <td>{{ c.expireDate }}</td>
                <td>
                  <span :class="c.daysLeft <= 7 ? 'bad' : ''">
                    {{ c.daysLeft }}
                  </span>
                </td>
              </tr>
              <tr v-if="data.certExpiring.length===0">
                <td colspan="5" style="opacity:.7">暂无</td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>

      <div class="card">
        <div style="display:flex;justify-content:space-between;align-items:center">
          <h4 style="margin:0">风险预警：最新等级 D/E 的工厂</h4>
          <span class="badge">Latest audit per factory</span>
        </div>
        <div style="overflow:auto;margin-top:10px">
          <table class="t">
            <thead>
              <tr>
                <th>工厂</th><th>最新年度</th><th>等级</th><th>逾期整改数</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="f in data.risk.deFactories" :key="f.factoryId">
                <td>{{ f.factoryCode }} - {{ f.factoryName }}</td>
                <td>{{ f.year }}</td>
                <td><span class="bad">{{ f.grade }}</span></td>
                <td>{{ f.overdueCapa }}</td>
              </tr>
              <tr v-if="!data.risk.deFactories || data.risk.deFactories.length===0">
                <td colspan="4" style="opacity:.7">暂无</td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>

      <div class="card">
        <h4 style="margin:0 0 8px 0">同一工厂历次评鉴对比（Demo API）</h4>
        <div style="opacity:.75">
          API：<code>/api/reports/factory-history/{factoryId}</code>（前端未做复杂图表，仅提供接口）。
          你也可以在后续把它做成“等级趋势折线 + 不合格条款数柱状图”。
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref } from 'vue'
import { http } from '../api/http'

const year = ref(new Date().getFullYear())
const days = ref(60)
const loading = ref(false)
const err = ref('')
const data = ref(null)

async function load() {
  err.value = ''
  loading.value = true
  try {
    const res = await http.get('/api/reports/overview', { params: { year: year.value, days: days.value } })
    data.value = res.data
  } catch (e) {
    err.value = e?.response?.data?.message || e?.message || String(e)
  } finally {
    loading.value = false
  }
}

load()
</script>

<style scoped>
.t{width:100%;border-collapse:collapse}
.t th,.t td{border:1px solid #eee;padding:8px;text-align:left;font-size:14px;white-space:nowrap}
.t th{background:#fafafa}
code{background:#f2f2f2;padding:2px 6px;border-radius:6px}
</style>
