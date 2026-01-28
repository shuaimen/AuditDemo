<template>
  <div class="card" v-if="audit">
    <div class="row">
      <div class="col">
        <div><b>{{ audit.factory.factoryCode }}</b> {{ audit.factory.factoryName }}</div>
        <div style="color:#666;font-size:13px">{{ audit.year }} / {{ audit.auditType===1?'年度评鉴':'新引入' }}</div>
      </div>
      <div class="col" style="display:flex;align-items:center;gap:8px;flex-wrap:wrap">
        <span class="badge">{{ audit.statusText }}</span>
        <span class="badge">等级：{{ audit.finalGrade || '-' }}</span>
        <span class="badge" v-if="audit.copiedFromAuditId">已复制去年数据</span>
      </div>
      <div class="col" style="display:flex;align-items:center;justify-content:end;gap:10px;flex-wrap:wrap">
        <button class="secondary" @click="refresh">刷新</button>
        <button @click="rate" :disabled="!canRate">判级并生成整改项</button>
        <router-link :to="'/audits/'+audit.auditId+'/capa'">整改项</router-link>
        <router-link :to="'/audits/'+audit.auditId+'/reaudits'">复评</router-link>
        <button class="secondary" v-if="isAdmin && audit.status!==2" @click="reopen">管理员重开</button>
      </div>
    </div>
    <div v-if="msg" class="bad" style="margin-top:10px">{{ msg }}</div>
  </div>

  <div class="card">
    <h3 style="margin:0 0 10px 0">模块进度</h3>
    <div v-if="!modules.length" style="color:#666">暂无模块</div>
    <div v-for="m in modules" :key="m.moduleId" class="card" style="background:#fbfbfe">
      <div class="row">
        <div class="col">
          <div><b>{{ m.moduleName }}</b></div>
          <div style="color:#666;font-size:13px">完成：{{ m.filled }}/{{ m.total }}；提交：{{ m.moduleStatus===2?'是':'否' }}</div>
        </div>
        <div class="col" style="display:flex;align-items:center;justify-content:end;gap:10px;flex-wrap:wrap">
          <router-link :to="'/audits/'+auditId+'/modules/'+m.moduleId">录入</router-link>
          <button class="secondary" v-if="m.moduleStatus!==2" @click="submit(m)">提交</button>
          <button class="secondary" v-else @click="withdraw(m)">撤回</button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { computed, onMounted, ref } from 'vue'
import { http } from '../api/http'
import { useRoute } from 'vue-router'

const route = useRoute()
const auditId = route.params.auditId

const audit = ref(null)
const modules = ref([])
const msg = ref('')

const isAdmin = computed(() => Number(localStorage.getItem('role') || 0) === 1)

const canRate = computed(() => {
  if (!audit.value) return false
  if (audit.value.status !== 2) return false
  return modules.value.length > 0 && modules.value.every(m => m.moduleStatus === 2)
})

async function refresh() {
  msg.value = ''
  const r = await http.get('/api/audits/' + auditId)
  audit.value = r.data.audit
  modules.value = r.data.modules
}

async function submit(m) {
  msg.value = ''
  try {
    await http.post(`/api/audits/${auditId}/modules/${m.moduleId}/submit`)
    await refresh()
  } catch (e) {
    msg.value = e?.response?.data?.message || '提交失败（请确认该模块条款已全部填写）'
  }
}

async function withdraw(m) {
  msg.value = ''
  try {
    await http.post(`/api/audits/${auditId}/modules/${m.moduleId}/withdraw`)
    await refresh()
  } catch (e) {
    msg.value = e?.response?.data?.message || '撤回失败'
  }
}

async function rate() {
  msg.value = ''
  try {
    const r = await http.post(`/api/audits/${auditId}/rate`)
    msg.value = `判级完成：${r.data.finalGrade}；${r.data.hasCapa ? '已生成整改项' : '无整改项'}`
    await refresh()
  } catch (e) {
    msg.value = e?.response?.data?.message || '判级失败（检查是否所有模块都已提交/条款已填写）'
  }
}

async function reopen() {
  msg.value = ''
  try {
    await http.post(`/api/audits/${auditId}/reopen`)
    msg.value = '已重开（等级与整改项已清空，模块回到“编辑中”）'
    await refresh()
  } catch (e) {
    msg.value = e?.response?.data?.message || '重开失败'
  }
}

onMounted(refresh)
</script>
