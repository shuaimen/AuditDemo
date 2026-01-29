<template>
  <div class="d-flex justify-content-between align-items-center mb-3">
    <div>
      <h5 class="mb-0">看板指标</h5>
      <div class="text-muted small">趋势 / 维度对比 / 闭环率 / 证照到期 / 风险预警</div>
    </div>
    <button class="btn btn-outline-secondary" @click="load" :disabled="busy">{{ busy ? '刷新中…' : '刷新' }}</button>
  </div>

  <div v-if="err" class="alert alert-danger">{{ err }}</div>

  <div class="row g-3">
    <div class="col-12 col-md-3">
      <div class="card card-soft shadow-sm">
        <div class="card-body">
          <div class="text-muted small">整改闭环率</div>
          <div class="display-6 fw-semibold">{{ pct(overview?.capaClosureRate) }}</div>
        </div>
      </div>
    </div>
    <div class="col-12 col-md-3">
      <div class="card card-soft shadow-sm">
        <div class="card-body">
          <div class="text-muted small">风险预警（条款）</div>
          <div class="display-6 fw-semibold">{{ overview?.riskCount ?? '-' }}</div>
        </div>
      </div>
    </div>
    <div class="col-12 col-md-3">
      <div class="card card-soft shadow-sm">
        <div class="card-body">
          <div class="text-muted small">证照即将到期</div>
          <div class="display-6 fw-semibold">{{ overview?.certExpiringCount ?? '-' }}</div>
        </div>
      </div>
    </div>
    <div class="col-12 col-md-3">
      <div class="card card-soft shadow-sm">
        <div class="card-body">
          <div class="text-muted small">本年评鉴数</div>
          <div class="display-6 fw-semibold">{{ overview?.auditCount ?? '-' }}</div>
        </div>
      </div>
    </div>

    <div class="col-12 col-lg-6">
      <div class="card card-soft shadow-sm">
        <div class="card-body">
          <h6>趋势</h6>
          <table class="table table-sm">
            <thead><tr><th>年度</th><th>A</th><th>B</th><th>C</th><th>D</th><th>E</th></tr></thead>
            <tbody>
              <tr v-for="r in (overview?.yearTrend||[])" :key="r.year">
                <td>{{ r.year }}</td>
                <td>{{ r.A }}</td><td>{{ r.B }}</td><td>{{ r.C }}</td><td>{{ r.D }}</td><td>{{ r.E }}</td>
              </tr>
              <tr v-if="(overview?.yearTrend||[]).length===0"><td colspan="6" class="text-muted">暂无数据（需后端实现 /api/reports/overview）</td></tr>
            </tbody>
          </table>
        </div>
      </div>
    </div>

    <div class="col-12 col-lg-6">
      <div class="card card-soft shadow-sm">
        <div class="card-body">
          <h6>维度对比（模块）</h6>
          <table class="table table-sm">
            <thead><tr><th>模块</th><th>不合格</th><th>部分不符合</th><th>闭环率</th></tr></thead>
            <tbody>
              <tr v-for="m in (overview?.moduleCompare||[])" :key="m.moduleId||m.moduleName">
                <td>{{ m.moduleName }}</td>
                <td>{{ m.ng }}</td>
                <td>{{ m.partial }}</td>
                <td>{{ pct(m.closureRate) }}</td>
              </tr>
              <tr v-if="(overview?.moduleCompare||[]).length===0"><td colspan="4" class="text-muted">暂无数据</td></tr>
            </tbody>
          </table>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { onMounted, ref } from 'vue'
import { ReportsApi } from '../api/endpoints'
import { apiErrorMessage } from '../api/http'

const busy = ref(false)
const err = ref('')
const overview = ref(null)

function pct(v) {
  if (v === null || v === undefined) return '-'
  const n = Number(v)
  if (Number.isNaN(n)) return '-'
  return `${Math.round(n*1000)/10}%`
}

async function load() {
  err.value = ''
  busy.value = true
  try {
    const data = await ReportsApi.overview({})
    overview.value = data
  } catch (e) {
    err.value = apiErrorMessage(e)
  } finally {
    busy.value = false
  }
}

onMounted(load)
</script>
